package com.zalchik.nimbus.engine

import com.zalchik.nimbus.util.parseObject
import com.zalchik.nimbus.util.str
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.OkHttpClient
import okhttp3.Request
import java.util.concurrent.Callable
import java.util.concurrent.Executors
import java.util.concurrent.Future
import java.util.concurrent.TimeUnit

/**
 * The address sites see while connected, and the country it belongs to — the port of
 * nimbus/exit_ip.py.
 *
 * The IP comes from our own endpoint (no limits, unlike free "what's my IP" APIs, which throttle
 * a VPN exit shared by many users), which also names the country from a fresh database.
 * Cloudflare's trace is asked only when ours is slow or down: VPN servers often send traffic for
 * Cloudflare out through another address than the rest.
 *
 * On a phone the requests need no proxy argument: while the VPN is up its own traffic goes
 * through the tunnel, so every check opens a new connection through it. A balancer sends each
 * new connection to the node it prefers at that moment, so this is what a site opened now would
 * see; a kept-open connection would go on showing the node picked when it was opened.
 */
object ExitIp {
    const val OWN = "https://zalchikmods.site/nimbus/ip"

    /** Seconds to give our endpoint before the fallbacks are asked as well. */
    const val HEDGE_AFTER_MS = 1200L

    private val FALLBACKS = listOf(
        "https://1.1.1.1/cdn-cgi/trace" to ::trace,
        "https://www.cloudflare.com/cdn-cgi/trace" to ::trace,
        "https://ipwho.is/" to ::jsonIp,
    )

    data class Answer(val ip: String, val country: String?, val own: Boolean)

    private fun jsonIp(body: String): Pair<String?, String?> {
        val json = parseObject(body) ?: return null to null
        return json.str("ip").takeIf { it.isNotEmpty() } to
            (json.str("country_code").ifEmpty { json.str("country") }.takeIf { it.isNotEmpty() })
    }

    private fun trace(body: String): Pair<String?, String?> {
        val fields = body.lines().filter { "=" in it }.associate { line ->
            line.substringBefore('=') to line.substringAfter('=')
        }
        return fields["ip"]?.takeIf { it.isNotEmpty() } to fields["loc"]?.takeIf { it.isNotEmpty() }
    }

    fun client(timeoutMs: Long = 5000): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(4, TimeUnit.SECONDS)
        .readTimeout(timeoutMs, TimeUnit.MILLISECONDS)
        .callTimeout(timeoutMs + 4000, TimeUnit.MILLISECONDS)
        .build()

    private fun ask(client: OkHttpClient, url: String, parse: (String) -> Pair<String?, String?>): Answer {
        val request = Request.Builder()
            .url(url)
            .header("User-Agent", "Nimbus")
            .header("Connection", "close")
            .header("Cache-Control", "no-cache")
            .build()
        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) throw java.io.IOException("HTTP ${response.code}")
            val body = response.body?.string() ?: ""
            val (ip, code) = parse(body)
            return Answer(normalizeIp(ip ?: ""), normalizeCountry(code), false)
        }
    }

    /** "" for an address Java's InetAddress won't take (a bracketed IPv6, say). */
    private fun normalizeIp(value: String): String {
        val text = value.trim().removeSurrounding("[", "]")
        return if (text.isEmpty()) "" else try {
            java.net.InetAddress.getByName(text).hostAddress ?: text
        } catch (e: Exception) {
            text
        }
    }

    private fun normalizeCountry(code: String?): String? {
        val value = (code ?: "").trim().uppercase()
        return if (value.length == 2 && value.all { it.isLetter() }) value else null
    }

    /** The first answer: ours, or a fallback's once ours proves slow. */
    private fun firstAnswer(client: OkHttpClient, timeoutMs: Long): Answer {
        val pool = Executors.newFixedThreadPool(1 + FALLBACKS.size)
        val errors = mutableListOf<String>()
        try {
            val primary: Future<Answer> = pool.submit(Callable {
                ask(client, OWN, ::jsonIp).let { it.copy(own = true) }
            })
            val own = try {
                primary.get(HEDGE_AFTER_MS, TimeUnit.MILLISECONDS)
            } catch (e: java.util.concurrent.TimeoutException) {
                null
            } catch (e: Exception) {
                errors.add(e.cause?.message ?: e.message ?: "own endpoint failed")
                null
            }
            if (own != null && own.ip.isNotEmpty()) return own

            val pending = mutableListOf<Future<Answer>>()
            if (own == null) pending.add(primary)
            for ((url, parse) in FALLBACKS) {
                pending.add(pool.submit(Callable { ask(client, url, parse) }))
            }
            val deadline = System.nanoTime() + TimeUnit.MILLISECONDS.toNanos(timeoutMs + 1000)
            while (pending.isNotEmpty() && System.nanoTime() < deadline) {
                val left = pending.toMutableList()
                for (future in pending) {
                    if (!future.isDone) continue
                    left.remove(future)
                    try {
                        val answer = future.get()
                        // Ours still wins if it answers in the same moment as a fallback.
                        if (answer.ip.isNotEmpty()) return answer.copy(own = future === primary)
                        errors.add("empty answer")
                    } catch (e: Exception) {
                        errors.add(e.cause?.message ?: e.message ?: "request failed")
                    }
                }
                pending.clear()
                pending.addAll(left)
                if (pending.isNotEmpty()) Thread.sleep(50)
            }
        } finally {
            pool.shutdownNow()
        }
        throw java.io.IOException(errors.joinToString("; ").ifEmpty { "timeout" })
    }

    /** Country of an IP by our endpoint's database, when the IP itself came from elsewhere. */
    fun ownCountry(ip: String, client: OkHttpClient = client(3000)): String? = try {
        val url = OWN.toHttpUrl().newBuilder().addQueryParameter("ip", ip).build()
        val request = Request.Builder().url(url)
            .header("User-Agent", "Nimbus")
            .header("Connection", "close")
            .build()
        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) return null
            normalizeCountry(parseObject(response.body?.string() ?: "")?.str("country"))
        }
    } catch (e: Exception) {
        null
    }

    /**
     * (IP, two-letter country code or null). Right after connecting the balancer may not have
     * picked a node yet, so a failed round is tried again a little later.
     */
    fun lookup(timeoutMs: Long = 5000, attempts: Int = 3, client: OkHttpClient = client(timeoutMs)): Answer {
        var answer: Answer? = null
        for (attempt in 0 until attempts) {
            try {
                answer = firstAnswer(client, timeoutMs)
                break
            } catch (e: Exception) {
                if (attempt == attempts - 1) throw e
                Thread.sleep(1000L + 2000L * attempt)
            }
        }
        val result = answer ?: throw java.io.IOException("timeout")
        val country = if (!result.own) ownCountry(result.ip, client) ?: result.country else result.country
        return result.copy(country = country)
    }

    /** The country of an IP as the connection sheet names it, without a check of its own. */
    fun countryName(code: String?, locale: java.util.Locale = java.util.Locale.getDefault()): String? {
        if (code == null) return null
        return try {
            java.util.Locale("", code).getDisplayCountry(locale).takeIf { it.isNotEmpty() && it != code }
        } catch (e: Exception) {
            null
        }
    }
}
