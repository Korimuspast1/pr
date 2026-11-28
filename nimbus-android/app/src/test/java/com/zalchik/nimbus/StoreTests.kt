package com.zalchik.nimbus

import com.zalchik.nimbus.data.Balancer
import com.zalchik.nimbus.data.Group
import com.zalchik.nimbus.data.Paths
import com.zalchik.nimbus.data.RoutePreset
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.data.Settings
import com.zalchik.nimbus.data.Store
import com.zalchik.nimbus.data.Subscription
import com.zalchik.nimbus.data.newId
import org.json.JSONObject
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNotSame
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.File

/**
 * settings.json and profiles.json are the desktop's files with the desktop's keys, so a folder
 * copied from a computer works on a phone and the other way round. These tests hold that claim:
 * everything survives a round trip, and a subscription update keeps the ids — and so the
 * selection and any balancer's member list — of the servers it already had.
 */
class StoreTests {
    private lateinit var dir: File

    @Before
    fun useATemporaryDataDir() {
        dir = File(System.getProperty("java.io.tmpdir"), "nimbus-store-test-${newId()}")
        Paths.useDataDir(dir)
    }

    @After
    fun cleanUp() {
        dir.deleteRecursively()
    }

    @Test
    fun profilesRoundTrip() {
        val store = Store(
            subscriptions = mutableListOf(
                Subscription(
                    id = "s1",
                    url = "https://panel.example.com/sub/abc",
                    name = "panel",
                    customName = "Моя",
                    userAgent = "Happ/3.0.0",
                    lastUpdated = 1700000000,
                    updateHours = 4,
                    total = 1024,
                    error = "",
                    sendHwid = true,
                )
            ),
            servers = mutableListOf(
                Server("a", "NL", "s1", Fixtures.LINKS.getValue("vless-reality"), null, 120),
                Server.ofConfig("b", "provider", "s1", Fixtures.PROVIDER),
                Server("c", "mine", "", Fixtures.LINKS.getValue("trojan")),
            ),
            groups = mutableListOf(Group("g1", "Added")),
            balancers = mutableListOf(
                Balancer("b1", "auto", mutableListOf("a", "c"), mutableListOf("s1"), 45, Balancer.MODE_FASTEST, 90)
            ),
        )
        store.save()

        val loaded = Store.load()
        assertEquals(1, loaded.subscriptions.size)
        assertEquals("Моя", loaded.subscriptions[0].customName)
        assertEquals(4, loaded.subscriptions[0].updateHours)
        assertTrue(loaded.subscriptions[0].sendHwid)
        assertEquals(3, loaded.servers.size)
        assertEquals(120, loaded.server("a")?.latency)
        assertNotNull(loaded.server("b")?.config)
        assertTrue(loaded.server("b")!!.isFullConfig)
        assertTrue(loaded.server("b")!!.config!!.has("outbounds"))
        assertEquals(1, loaded.groups.size)
        val balancer = loaded.balancer("b1")!!
        assertEquals(listOf("a", "c"), balancer.serverIds)
        assertEquals(listOf("s1"), balancer.subIds)
        assertEquals(45, balancer.interval)
        assertFalse(balancer.isStable)
        assertEquals(90, balancer.latency)
    }

    @Test
    fun serverProtocolIsReadFromEitherForm() {
        assertEquals("vless", Server("x", "n", link = Fixtures.LINKS.getValue("vless-reality")).protocol)
        assertEquals("hysteria2", Server("x", "n", link = Fixtures.LINKS.getValue("hy2")).protocol)
        assertEquals("config", Server.ofConfig("x", "n", "", JSONObject("""{"outbounds":[]}""")).protocol)
        // A provider's own balancer is worth naming as one in the list's badge.
        assertTrue(Fixtures.configServer().protocol.startsWith("balancer"))
        // Helpers don't count as the protocol a server speaks.
        val helpers = Server.ofConfig(
            "x",
            "n",
            "",
            JSONObject("""{"outbounds":[{"protocol":"freedom"},{"protocol":"vless"}]}"""),
        )
        assertEquals("vless", helpers.protocol)
    }

    @Test
    fun membersComeFromIdsAndWholeSubscriptions() {
        val store = Store(
            servers = mutableListOf(
                Server("a", "A", "s1"), Server("b", "B", "s1"), Server("c", "C", "")
            ),
            subscriptions = mutableListOf(Subscription("s1", "https://x/y")),
        )
        assertEquals(listOf("a", "b"), store.members(Balancer("b", "sub", subIds = mutableListOf("s1"))).map { it.id })
        assertEquals(listOf("c"), store.members(Balancer("b", "one", serverIds = mutableListOf("c"))).map { it.id })
        // Both, and no duplicates when a server is in a picked subscription and picked by hand.
        val both = store.members(Balancer("b", "both", mutableListOf("a", "c"), mutableListOf("s1")))
        assertEquals(listOf("a", "b", "c"), both.map { it.id })
    }

    @Test
    fun aSubscriptionUpdateKeepsItsIds() {
        val store = Store(
            subscriptions = mutableListOf(Subscription("s1", "https://x/y")),
            servers = mutableListOf(
                Server("keep", "NL", "s1", "trojan://a@1.1.1.1:443", null, 42),
                Server("gone", "Old", "s1", "trojan://b@2.2.2.2:443"),
            ),
        )
        val incoming = listOf(
            Server(newId(), "NL", "s1", "trojan://a@1.1.1.1:443"),
            Server(newId(), "New", "s1", "trojan://c@3.3.3.3:443"),
        )
        store.replaceServers("s1", incoming)

        val names = store.servers.map { it.name }
        assertEquals(listOf("NL", "New"), names)
        // The same-named server kept its id and its measured delay, so the selection survives.
        assertEquals("keep", store.servers[0].id)
        assertEquals(42, store.servers[0].latency)
        assertTrue(store.server("gone") == null)

        // Servers of other subscriptions are untouched, and the position in the list is kept.
        store.servers.add(Server("other", "Other", "s2"))
        store.replaceServers("s1", listOf(Server(newId(), "NL", "s1", "trojan://a@1.1.1.1:443")))
        assertEquals(listOf("keep", "other"), store.servers.map { it.id })
    }

    @Test
    fun removingTakesTheServersWithIt() {
        val store = Store(
            subscriptions = mutableListOf(Subscription("s1", "https://x/y")),
            servers = mutableListOf(Server("a", "A", "s1"), Server("b", "B", "g1")),
            groups = mutableListOf(Group("g1", "Added")),
            balancers = mutableListOf(Balancer("b1", "auto", serverIds = mutableListOf("a"))),
        )
        store.removeSubscription("s1")
        assertNull(store.server("a"))
        assertNotNull(store.server("b"))

        store.removeGroup("g1")
        assertNull(store.group("g1"))
        assertNull(store.server("b"))
        // The balancer stays, but its members are gone — the UI then shows it as empty.
        assertTrue(store.members(store.balancer("b1")!!).isEmpty())
    }

    @Test
    fun recentsAreCapped() {
        val settings = Settings()
        val store = Store()
        for (i in 1..12) store.pushRecent(settings, "id$i")
        assertEquals(8, settings.recentServers.size)
        assertEquals("id12", settings.recentServers.first())
        // Picking a server again moves it to the front instead of repeating it.
        store.pushRecent(settings, "id9")
        assertEquals("id9", settings.recentServers.first())
        assertEquals(8, settings.recentServers.size)
        assertEquals(1, settings.recentServers.count { it == "id9" })
    }

    @Test
    fun anEmptyOrBrokenFileGivesAnEmptyStore() {
        assertTrue(Store.load().servers.isEmpty())
        Paths.profilesFile.writeText("not json at all")
        assertTrue(Store.load().servers.isEmpty())
        Paths.profilesFile.writeText("{}")
        assertTrue(Store.load().servers.isEmpty())
    }

    // -- settings ---------------------------------------------------------------------------------

    @Test
    fun settingsRoundTrip() {
        val settings = Settings().apply {
            tunnelMode = "proxy"
            killSwitch = true
            tunMtu = 1400
            perAppEnabled = true
            perAppMode = RoutePreset.MODE_BYPASS
            perAppPackages = mutableListOf("com.example.app")
            routingMode = "bypass_ru"
            useProviderRouting = false
            blockAds = false
            bypassPrivate = false
            domainStrategy = "IPOnDemand"
            remoteDns = "tls://dns.example"
            directDns = "1.1.1.1"
            fakeDns = false
            sniffing = false
            mux = true
            muxConcurrency = 3
            fragment = true
            fragmentLength = "10-50"
            logLevel = "debug"
            subUserAgent = "Custom/1.0"
            subAutoUpdate = false
            subUpdateHours = 12
            hwidMode = "never"
            hwidCustom = "abc"
            pingTimeout = 9
            autoConnect = true
            autostart = true
            balancerNotify = false
            updateCheck = false
            updateSkip = "0.9.9"
            theme = "light"
            language = "en"
            selectedServer = "a"
            recentServers = mutableListOf("a", "b")
            sortMode = "ping"
            groupSort = mutableMapOf("s1" to "name")
            routePresets = mutableListOf(
                RoutePreset(
                    "p1",
                    "Work",
                    RoutePreset.MODE_VPN,
                    provider = false,
                    packages = mutableListOf("com.work.app"),
                    sites = mutableListOf("work.example"),
                )
            )
            routePreset = "p1"
        }
        settings.save()

        val loaded = Settings.load()
        assertEquals(settings.toJson().toString(), loaded.toJson().toString())
        assertEquals("proxy", loaded.tunnelMode)
        assertTrue(loaded.killSwitch)
        assertEquals(1400, loaded.tunMtu)
        assertEquals(listOf("com.example.app"), loaded.perAppPackages)
        assertEquals("IPOnDemand", loaded.domainStrategy)
        assertEquals("en", loaded.language)
        assertEquals("light", loaded.theme)
        assertEquals(listOf("a", "b"), loaded.recentServers)
        assertEquals("name", loaded.groupSort["s1"])
        assertEquals(Settings.SETTINGS_VERSION, loaded.settingsVersion)

        val preset = loaded.activePreset
        assertNotNull(preset)
        assertEquals("Work", preset!!.name)
        assertTrue(preset.isVpnOnly)
        assertFalse(preset.provider)
        assertEquals(listOf("work.example"), preset.sites)
        assertEquals(listOf("com.work.app"), preset.packages)
    }

    @Test
    fun unknownKeysAreIgnoredAndDefaultsKept() {
        // A settings.json from a newer desktop, or one written by the desktop with keys a phone
        // has no use for, must load rather than reset the user's choices.
        Paths.settingsFile.writeText(
            """
            {"tunnel_mode": "tun", "local_port": 1080, "tray": true,
             "routing_mode": "bypass_ru", "something_new": {"a": 1},
             "route_presets": [{"id":"p1","name":"Desktop","apps":[{"name":"chrome.exe"}],
                                "sites":["example.ru"]}]}
            """.trimIndent()
        )
        val loaded = Settings.load()
        assertEquals("bypass_ru", loaded.routingMode)
        assertEquals("https://1.1.1.1/dns-query", loaded.remoteDns)
        assertEquals(Settings.SETTINGS_VERSION, loaded.settingsVersion)
        val preset = loaded.routePresets.single()
        // A desktop preset names programs by path; a phone names them by package, so the program
        // list is dropped and the sites are kept.
        assertTrue(preset.packages.isEmpty())
        assertEquals(listOf("example.ru"), preset.sites)
    }

    @Test
    fun aBrokenSettingsFileGivesDefaults() {
        Paths.settingsFile.writeText("{")
        assertEquals("tun", Settings.load().tunnelMode)
        Paths.settingsFile.delete()
        assertEquals(Settings(), Settings.load())
    }

    @Test
    fun writesAreAtomic() {
        Paths.writeAtomic(Paths.settingsFile, "{\"a\":1}")
        assertEquals("{\"a\":1}", Paths.settingsFile.readText())
        assertFalse(File(Paths.settingsFile.parentFile, "settings.json.tmp").exists())
        // Writing over an existing file replaces it rather than appending.
        Paths.writeAtomic(Paths.settingsFile, "{\"b\":2}")
        assertEquals("{\"b\":2}", Paths.settingsFile.readText())
    }

    @Test
    fun aCopyIsANewInstanceWithTheSameContents() {
        // The UI holds settings and the store as Compose state, which only repaints when the
        // instance changes: copy() gives it a new one after an in-place edit. The lists stay
        // shared, which is what makes editing a package list and saving it one step.
        val settings = Settings().apply { perAppPackages = mutableListOf("a") }
        val copy = settings.copy()
        assertNotSame(settings, copy)
        assertEquals(settings.toJson().toString(), copy.toJson().toString())
        copy.perAppPackages.add("b")
        assertEquals(listOf("a", "b"), settings.perAppPackages)

        val store = Store(servers = mutableListOf(Server("x", "X")))
        val storeCopy = store.copy()
        assertNotSame(store, storeCopy)
        storeCopy.servers.add(Server("y", "Y"))
        assertEquals(2, store.servers.size)
    }
}
