package com.zalchik.nimbus

import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.i18n.LocaleEn
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Test
import java.io.File
import java.util.Locale

/**
 * Russian is the source language, so every string in the app is written in Russian and looked up
 * in [LocaleEn] when the interface is English. A text missing from the table shows in Russian on
 * an English phone, which is the one i18n bug nobody notices until a user writes about it — so
 * the catalogue is checked against the sources, as the desktop checks it against nimbus/.
 */
class I18nTests {
    @Test
    fun trAndPlural() {
        val old = I18n.lang
        try {
            I18n.setLanguage("ru")
            assertEquals("Добавлено серверов: 3", I18n.tr("Добавлено серверов: {n}", values = mapOf("n" to 3)))
            assertEquals(
                listOf("сервер", "сервера", "серверов", "серверов", "сервер", "серверов"),
                listOf(1, 2, 5, 11, 21, 112).map { I18n.plural(it, "сервер", "сервера", "серверов") },
            )
            // A text with no translation stays as it is rather than turning into a key.
            assertEquals("Текста нет в словаре", I18n.tr("Текста нет в словаре"))

            I18n.setLanguage("en")
            assertEquals("Servers added: 3", I18n.tr("Добавлено серверов: {n}", values = mapOf("n" to 3)))
            assertEquals("Текста нет в словаре", I18n.tr("Текста нет в словаре"))
            assertEquals(
                listOf("server", "servers", "servers"),
                listOf(1, 2, 21).map { I18n.plural(it, "сервер", "сервера", "серверов") },
            )
        } finally {
            I18n.setLanguage(old)
        }
    }

    @Test
    fun languageChoice() {
        assertEquals("en", I18n.resolve("en"))
        assertEquals("ru", I18n.resolve("ru"))
        assertTrue(I18n.LANGUAGES.containsKey(I18n.resolve("auto")))
        assertEquals("auto", com.zalchik.nimbus.data.Settings().language)
        // "auto" follows the system: Russian-speaking ones get Russian, the rest English.
        assertEquals("ru", I18n.systemLanguage(Locale("ru", "RU")))
        assertEquals("ru", I18n.systemLanguage(Locale("be", "BY")))
        assertEquals("en", I18n.systemLanguage(Locale.UK))
        assertEquals("en", I18n.systemLanguage(Locale("pl", "PL")))
        assertEquals("ru", I18n.resolve("auto", Locale("kk", "KZ")))
        assertEquals("en", I18n.resolve("auto", Locale.GERMANY))
    }

    @Test
    fun englishCoversEveryText() {
        val root = sourceRoot()
        assumeTrue("sources not on disk", root != null)
        val (texts, plurals) = sourceTexts(root!!)
        assertTrue("found only ${texts.size} texts", texts.size > 250)

        val untranslated = (texts - LocaleEn.TEXTS.keys).sorted()
        assertEquals("texts without an English translation", emptyList<String>(), untranslated)

        val noPlural = (plurals - LocaleEn.PLURALS.keys).sorted()
        assertEquals("plural forms without English", emptyList<String>(), noPlural)
    }

    @Test
    fun translationsKeepTheirPlaceholdersAndLoseTheirCyrillic() {
        val cyrillic = Regex("[А-Яа-яЁё]")
        val placeholder = Regex("\\{(\\w+)}")
        for ((russian, english) in LocaleEn.TEXTS) {
            assertEquals(
                "placeholders of «$russian»",
                placeholder.findAll(russian).map { it.groupValues[1] }.toList().sorted(),
                placeholder.findAll(english).map { it.groupValues[1] }.toList().sorted(),
            )
            assertFalse("cyrillic left in the English of «$russian»", cyrillic.containsMatchIn(english))
            assertTrue("empty English for «$russian»", english.isNotBlank())
        }
        for ((russian, forms) in LocaleEn.PLURALS) {
            val want = placeholder.findAll(russian).map { it.groupValues[1] }.toList().sorted()
            assertEquals(want, placeholder.findAll(forms.first).map { it.groupValues[1] }.toList().sorted())
            assertEquals(want, placeholder.findAll(forms.second).map { it.groupValues[1] }.toList().sorted())
        }
    }

    @Test
    fun contextsTellHomonymsApart() {
        // One Russian text can mean two things; the table keys those as "<ctx>|<text>".
        val contextual = LocaleEn.TEXTS.keys.filter { '|' in it }
        for (key in contextual) {
            val text = key.substringAfter('|')
            I18n.setLanguage("en")
            assertEquals(LocaleEn.TEXTS.getValue(key), I18n.tr(text, key.substringBefore('|')))
        }
        I18n.setLanguage("ru")
    }

    companion object {
        /**
         * A double quote. The patterns below are raw strings, and a raw string can't end with a
         * quote of its own (Kotlin reads the closing delimiter first), so the quote is a template.
         */
        private const val Q = "\""

        /** Every tr("…") text and plural() "one" form in the app's own sources. */
        private fun sourceTexts(root: File): Pair<Set<String>, Set<String>> {
            val texts = LinkedHashSet<String>()
            val plurals = LinkedHashSet<String>()
            val tr = Regex("""\btr\(\s*$Q((?:[^$Q\\]|\\.)*)$Q""")
            val withContext = Regex("""\btr\(\s*$Q((?:[^$Q\\]|\\.)*)$Q\s*,\s*$Q((?:[^$Q\\]|\\.)*)$Q""")
            val plural = Regex("""\bplural\(\s*[^,]+,\s*$Q((?:[^$Q\\]|\\.)*)$Q""")
            root.walkTopDown()
                .filter { it.isFile && it.extension == "kt" }
                .forEach { file ->
                    val source = file.readText(Charsets.UTF_8)
                    for (match in tr.findAll(source)) {
                        val text = unescape(match.groupValues[1])
                        // A template isn't a catalogue key: it is built at run time.
                        if ("\${" in match.groupValues[1]) continue
                        texts.add(text)
                    }
                    for (match in withContext.findAll(source)) {
                        if ("\${" in match.groupValues[1]) continue
                        texts.add("${unescape(match.groupValues[2])}|${unescape(match.groupValues[1])}")
                    }
                    for (match in plural.findAll(source)) plurals.add(unescape(match.groupValues[1]))
                }
            return texts to plurals
        }

        private fun unescape(text: String): String = text
            .replace("\\\"", "\"")
            .replace("\\n", "\n")
            .replace("\\t", "\t")
            .replace("\\$", "$")
            .replace("\\\\", "\\")

        /** The app's source folder, found from wherever Gradle put the test's working directory. */
        private fun sourceRoot(): File? {
            var dir = File(System.getProperty("user.dir"))
            repeat(4) {
                val candidate = File(dir, "src/main/java")
                if (candidate.isDirectory) return candidate
                val nested = File(dir, "app/src/main/java")
                if (nested.isDirectory) return nested
                dir = dir.parentFile ?: return null
            }
            return null
        }
    }
}
