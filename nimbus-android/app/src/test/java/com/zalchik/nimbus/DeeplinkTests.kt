package com.zalchik.nimbus

import com.zalchik.nimbus.engine.Deeplink
import com.zalchik.nimbus.util.percentEncode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Links other apps hand over. On a phone these arrive as intents rather than command-line
 * arguments, but the formats are the desktop's — the same panels and the same clients produce
 * them — so the same strings must resolve the same way.
 */
class DeeplinkTests {
    private val sub = "https://panel.example.com/sub/AbC?x=1&y=2"
    private val encoded get() = percentEncode(sub)

    @Test
    fun subscriptionFormats() {
        for (text in listOf(
            "nimbus://import/$sub",
            "nimbus://import/$encoded",
            "NIMBUS://import?url=$encoded",
            "happ://add/$sub",
            "v2rayn://install-sub?url=$encoded&name=My",
            "v2rayng://install-sub?url=$encoded",
            "clash://install-config?url=$encoded",
            "clashmeta://install-config?url=$encoded",
            "sing-box://import-remote-profile?url=$encoded#My",
        )) {
            val link = Deeplink.parse(text)
            assertEquals(text, "subscription", link?.kind)
            assertEquals(text, sub, link?.payload)
        }
        assertEquals("My", Deeplink.parse("v2rayn://install-sub?url=$encoded&name=My")?.name)
        assertEquals("My", Deeplink.parse("sing-box://import-remote-profile?url=$encoded#My")?.name)
    }

    @Test
    fun configsCryptAndOthers() {
        val vless = Fixtures.LINKS.getValue("vless-reality")
        assertEquals("configs", Deeplink.parse("nimbus://import/" + percentEncode(vless))?.kind)
        assertEquals("configs", Deeplink.parse("nimbus://add/$vless")?.kind)

        for (crypt in listOf("happ://crypt/abcdef", "happ://crypt2/abc", "happ://crypt5/xyz", "HAPP://CRYPT3/q")) {
            // An encrypted link Nimbus can't open still says so, instead of looking invalid.
            assertEquals(crypt, "crypt", Deeplink.parse(crypt)?.kind)
        }

        assertEquals("invalid", Deeplink.parse("nimbus://import/not a link")?.kind)
        assertEquals("invalid", Deeplink.parse("v2rayn://install-sub")?.kind)
        assertEquals("invalid", Deeplink.parse("nimbus://unknown/x")?.kind)

        // A plain URL and a plain share link are not deep links: they are what one carries.
        assertNull(Deeplink.parse(sub))
        assertNull(Deeplink.parse(vless))
        assertNull(Deeplink.parse(""))
        assertNull(Deeplink.parse(null))
    }

    @Test
    fun theSchemesTheManifestDeclaresAreTheOnesParsed() {
        for (scheme in Deeplink.SCHEMES) {
            assertTrue(scheme, Deeplink.isDeepLink("$scheme://x"))
        }
        assertTrue(Deeplink.isDeepLink("NIMBUS://import/x"))
        assertTrue(!Deeplink.isDeepLink("https://example.com"))
        assertTrue(!Deeplink.isDeepLink("not a link"))
        assertEquals("nimbus", Deeplink.SCHEME)
    }
}
