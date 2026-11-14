"""Тесты интерпретатора «Смайлики»."""

import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from smiley import (  # noqa: E402
    Lexer,
    Parser,
    SmileyError,
    execute,
    run_to_string,
    to_text,
)


class LexerTests(unittest.TestCase):
    def test_emoji_and_ascii_keywords_are_the_same(self):
        def kinds(source):
            return [token.kind for token in Lexer(source).tokenize() if token.kind != "EOF"]

        self.assertEqual(kinds("📦 x ⬅ 1"), kinds("let x = 1"))
        self.assertEqual(kinds("📦 x ⬅ 1"), kinds("пусть x = 1"))

    def test_variation_selector_is_ignored(self):
        with_selector = Lexer("✖️ 2").tokenize()[0].kind  # ✖ + U+FE0F
        without = Lexer("✖ 2").tokenize()[0].kind
        self.assertEqual(with_selector, "STAR")
        self.assertEqual(without, "STAR")

    def test_strings(self):
        tokens = Lexer(r'🗣 "а\n\"б\"" и «ёж»').tokenize()
        strings = [t.value for t in tokens if t.kind == "STR"]
        self.assertEqual(strings, ['а\n"б"', "ёж"])

    def test_numbers(self):
        values = [t.value for t in Lexer("1 2.5 1_000").tokenize() if t.kind == "NUM"]
        self.assertEqual(values, [1, 2.5, 1000])

    def test_comments(self):
        source = '📦 x ⬅ 1 🙈 комментарий\n# ещё\n// и ещё\n🗣 x'
        self.assertEqual(run_to_string(source), "1")

    def test_unknown_character(self):
        with self.assertRaises(SmileyError):
            Lexer("🗣 @").tokenize()


class ParserTests(unittest.TestCase):
    def test_precedence(self):
        self.assertEqual(run_to_string("🗣 2 ➕ 3 ✖ 4 ➖ 1"), "13")
        self.assertEqual(run_to_string("🗣 (2 ➕ 3) ✖ 4"), "20")

    def test_missing_end(self):
        with self.assertRaises(SmileyError) as ctx:
            execute("🔂 3\n🗣 1")
        self.assertIn("🔚", str(ctx.exception))

    def test_unclosed_string(self):
        with self.assertRaises(SmileyError) as ctx:
            execute('🗣 "привет')
        self.assertIn("кавычк", str(ctx.exception))


class InterpreterTests(unittest.TestCase):
    def test_hello(self):
        self.assertEqual(run_to_string('🗣 "Привет 😊"'), "Привет 😊")

    def test_variables_and_reassignment(self):
        source = "📦 x ⬅ 1\nx ⬅ x ➕ 41\n🗣 x"
        self.assertEqual(run_to_string(source), "42")

    def test_if_else(self):
        source = '📦 n ⬅ 5\n❓ n ⬆ 3 🤔\n🗣 "больше"\n🙃\n🗣 "меньше"\n🔚'
        self.assertEqual(run_to_string(source), "больше")

    def test_else_if_chain(self):
        source = "\n".join(
            [
                "📦 n ⬅ 2",
                '❓ n 🟰 1 🤔 🗣 "один"',
                "🙃 ❓ n 🟰 2 🤔 🗣 \"два\"",
                '🙃 🗣 "много"',
                "🔚",
            ]
        )
        self.assertEqual(run_to_string(source), "два")

    def test_repeat(self):
        self.assertEqual(run_to_string('🔂 3\n🗣 "😊"\n🔚'), "😊\n😊\n😊")

    def test_while_and_break(self):
        source = "\n".join(
            [
                "📦 i ⬅ 0",
                "🔁 ✅",
                "i ⬅ i ➕ 1",
                "❓ i ⬆ 2 🤔 💔 🔚",
                "🗣 i",
                "🔚",
            ]
        )
        self.assertEqual(run_to_string(source), "1\n2")

    def test_continue(self):
        source = "\n".join(
            [
                "📦 i ⬅ 0",
                "🔁 i ⬇ 5",
                "i ⬅ i ➕ 1",
                "❓ i 📐 2 🟰 0 🤔 ⏭ 🔚",
                "🗣 i",
                "🔚",
            ]
        )
        self.assertEqual(run_to_string(source), "1\n3\n5")

    def test_functions_and_recursion(self):
        source = "\n".join(
            [
                "🎁 факт(n)",
                "❓ n ⬇🟰 1 🤔",
                "↩ 1",
                "🔚",
                "↩ n ✖ факт(n ➖ 1)",
                "🔚",
                "🗣 факт(6)",
            ]
        )
        self.assertEqual(run_to_string(source), "720")

    def test_function_without_return_gives_none(self):
        self.assertEqual(run_to_string("🎁 f()\n🗣 1\n🔚\n🗣 f()"), "1\n🤷")

    def test_closures_see_outer_scope(self):
        source = "\n".join(["📦 a ⬅ 10", "🎁 f()", "↩ a ✖ 2", "🔚", "🗣 f()"])
        self.assertEqual(run_to_string(source), "20")

    def test_booleans_render_as_emoji(self):
        self.assertEqual(run_to_string("🗣 ✅\n🗣 ❌"), "✅\n❌")

    def test_logic(self):
        source = "🗣 ✅ 🤝 ❌\n🗣 ✅ 🔀 ❌\n🗣 🙅 ❌"
        self.assertEqual(run_to_string(source), "❌\n✅\n✅")

    def test_short_circuit(self):
        # правая часть не должна вычисляться — иначе было бы деление на ноль
        self.assertEqual(run_to_string("🗣 ❌ 🤝 (1 ➗ 0)"), "❌")
        self.assertEqual(run_to_string("🗣 ✅ 🔀 (1 ➗ 0)"), "✅")

    def test_string_concatenation_and_repeat(self):
        self.assertEqual(run_to_string('🗣 "а" ➕ 1'), "а1")
        self.assertEqual(run_to_string('🗣 "ха" ✖ 3'), "хахаха")

    def test_division(self):
        self.assertEqual(run_to_string("🗣 8 ➗ 2"), "4")
        self.assertEqual(run_to_string("🗣 7 ➗ 2"), "3.5")

    def test_division_by_zero(self):
        with self.assertRaises(SmileyError) as ctx:
            execute("🗣 1 ➗ 0")
        self.assertIn("ноль", str(ctx.exception))

    def test_modulo(self):
        self.assertEqual(run_to_string("🗣 7 📐 3"), "1")

    def test_comparisons(self):
        source = "🗣 1 ⬇ 2\n🗣 1 ⬆🟰 1\n🗣 2 ⬇🟰 1\n🗣 1 🚫 2"
        self.assertEqual(run_to_string(source), "✅\n✅\n❌\n✅")

    def test_builtin_len(self):
        self.assertEqual(run_to_string('🗣 длина("ёжик")'), "4")

    def test_numbers_from_strings(self):
        self.assertEqual(run_to_string('🗣 число("12") ➕ 1'), "13")

    def test_random_is_deterministic_with_seed(self):
        source = "🗣 случайное(1, 100)"
        first = run_to_string(source, seed=7)
        second = run_to_string(source, seed=7)
        self.assertEqual(first, second)

    def test_input(self):
        answers = iter(["Аня"])
        result = execute('📦 имя ⬅ спросить("Как тебя зовут? ")\n🗣 "Привет, " ➕ имя', stdin=lambda p: next(answers))
        self.assertEqual(result.output, ["Привет, Аня"])

    def test_undefined_variable(self):
        with self.assertRaises(SmileyError) as ctx:
            execute("🗣 нет_такой")
        self.assertIn("не найдена", str(ctx.exception))

    def test_break_outside_loop(self):
        with self.assertRaises(SmileyError):
            execute("💔")

    def test_return_outside_function(self):
        with self.assertRaises(SmileyError):
            execute("↩ 1")

    def test_step_limit(self):
        with self.assertRaises(SmileyError) as ctx:
            execute("🔁 ✅\n🔚", max_steps=1000)
        self.assertIn("слишком долго", str(ctx.exception))

    def test_output_limit(self):
        with self.assertRaises(SmileyError) as ctx:
            execute('🔁 ✅\n🗣 "x"\n🔚', max_output_lines=50)
        self.assertIn("много вывода", str(ctx.exception))

    def test_output_callback(self):
        seen = []
        execute("🗣 1\n🗣 2", out=seen.append)
        self.assertEqual(seen, ["1", "2"])

    def test_to_text(self):
        self.assertEqual(to_text(True), "✅")
        self.assertEqual(to_text(3.0), "3")
        self.assertEqual(to_text(None), "🤷")

    def test_wrong_argument_count(self):
        with self.assertRaises(SmileyError) as ctx:
            execute("🎁 f(a)\n↩ a\n🔚\n🗣 f(1, 2)")
        self.assertIn("ждёт 1 аргумент", str(ctx.exception))

    def test_arity_of_builtin(self):
        with self.assertRaises(SmileyError):
            execute("🗣 длина()")

    def test_comments_only_program(self):
        self.assertEqual(run_to_string("🙈 просто комментарий\n# и ещё"), "")

    def test_russian_keywords(self):
        source = "\n".join(["пусть x = 2", "если x > 1 то", 'сказать "да"', "иначе", 'сказать "нет"', "конец"])
        self.assertEqual(run_to_string(source), "да")


if __name__ == "__main__":
    unittest.main(verbosity=2)
