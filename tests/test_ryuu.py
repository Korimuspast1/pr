"""Тесты референс-реализации RYU (Ryuu Notation).

Запуск:  python3 -m unittest discover -s tests -v
"""

import base64
import io
import json
import math
import os
import sys
import tempfile
import unittest
from datetime import date, datetime, time, timezone

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ryuu
from ryuu import Link, RyuuParseError, RyuuWriteError
from ryuu.__main__ import main as cli_main


def parse_err(text):
    """Разбор должен упасть с RyuuParseError."""
    try:
        ryuu.loads(text)
    except RyuuParseError:
        return True
    return False


class TestScalars(unittest.TestCase):
    def test_string(self):
        self.assertEqual(ryuu.loads("~ hello world"), "hello world")
        self.assertEqual(ryuu.loads("~"), "")
        self.assertEqual(ryuu.loads("~   spaced"), "  spaced")  # один пробел съедается

    def test_string_keeps_trailing_space(self):
        self.assertEqual(ryuu.loads("~ x  "), "x  ")

    def test_string_with_runes_inside(self):
        self.assertEqual(ryuu.loads("~ . + ' # ? ; внутри строки"), ". + ' # ? ; внутри строки")

    def test_numbers(self):
        self.assertEqual(ryuu.loads("# 42"), 42)
        self.assertEqual(ryuu.loads("# -7"), -7)
        self.assertEqual(ryuu.loads("# +5"), 5)
        self.assertEqual(ryuu.loads("# 007"), 7)
        self.assertEqual(ryuu.loads("# 1_000_000"), 1_000_000)
        self.assertEqual(ryuu.loads("# 3.14"), 3.14)
        self.assertEqual(ryuu.loads("# -0.5"), -0.5)
        self.assertEqual(ryuu.loads("# .5"), 0.5)
        self.assertEqual(ryuu.loads("# 1e3"), 1000.0)
        self.assertEqual(ryuu.loads("# 1.5e-3"), 0.0015)
        self.assertEqual(ryuu.loads("# 0x1F"), 31)
        self.assertEqual(ryuu.loads("# 0b1010"), 10)
        self.assertEqual(ryuu.loads("# 0o17"), 15)
        self.assertTrue(math.isnan(ryuu.loads("# nan")))
        self.assertEqual(ryuu.loads("# inf"), math.inf)
        self.assertEqual(ryuu.loads("# -inf"), -math.inf)

    def test_bad_numbers(self):
        for t in ("# 1.", "# --1", "# 0x", "# 1.2.3", "# abc", "#", "# 1e"):
            self.assertTrue(parse_err(t), t)

    def test_booleans(self):
        for t, v in (
            ("? yes", True), ("? true", True), ("? on", True), ("? 1", True),
            ("? YES", True), ("? no", False), ("? false", False), ("? off", False),
            ("? 0", False),
        ):
            self.assertEqual(ryuu.loads(t), v, t)

    def test_bad_boolean(self):
        for t in ("? maybe", "?", "? да"):
            self.assertTrue(parse_err(t), t)

    def test_void(self):
        self.assertIsNone(ryuu.loads("!"))

    def test_void_with_payload_is_error(self):
        self.assertTrue(parse_err("! x"))

    def test_temporal(self):
        self.assertEqual(ryuu.loads("% 2026-09-28"), date(2026, 9, 28))
        self.assertEqual(ryuu.loads("% 14:30"), time(14, 30))
        self.assertEqual(ryuu.loads("% 14:30:59"), time(14, 30, 59))
        self.assertEqual(
            ryuu.loads("% 2026-09-28T14:30:59Z"),
            datetime(2026, 9, 28, 14, 30, 59, tzinfo=timezone.utc),
        )
        self.assertEqual(
            ryuu.loads("% 2026-09-28T14:30:59+03:00"),
            datetime.fromisoformat("2026-09-28T14:30:59+03:00"),
        )
        self.assertTrue(parse_err("% 2026-13-45"))
        self.assertTrue(parse_err("% yesterday"))

    def test_link(self):
        v = ryuu.loads("& https://example.com/a?b=1")
        self.assertIsInstance(v, Link)
        self.assertEqual(v, "https://example.com/a?b=1")
        self.assertTrue(parse_err("&"))

    def test_bytes(self):
        raw = b"\x00\x01hello\xff"
        t = ryuu.dumps(raw)
        self.assertEqual(t.strip(), "$ " + base64.b64encode(raw).decode())
        self.assertEqual(ryuu.loads(t), raw)
        self.assertTrue(parse_err("$ ???"))

    def test_hollow(self):
        self.assertEqual(ryuu.loads("| map"), {})
        self.assertEqual(ryuu.loads("| list"), [])
        self.assertTrue(parse_err("| set"))
        self.assertTrue(parse_err("|"))

    def test_root_atom_types(self):
        self.assertEqual(ryuu.loads("~ 42"), "42")
        self.assertEqual(ryuu.loads("# 42"), 42)


class TestStructure(unittest.TestCase):
    def test_flat_map(self):
        self.assertEqual(
            ryuu.loads(". a ~ 1\n. b # 2\n. c ? yes"),
            {"a": "1", "b": 2, "c": True},
        )

    def test_field_and_atom_lines(self):
        self.assertEqual(
            ryuu.loads(". name\n~ Alice\n. age\n# 30"),
            {"name": "Alice", "age": 30},
        )

    def test_nested_maps(self):
        self.assertEqual(
            ryuu.loads(". a\n. b\n. c ~ x\n'\n. d # 1"),
            {"a": {"b": {"c": "x"}, "d": 1}},
        )

    def test_list_of_scalars(self):
        self.assertEqual(ryuu.loads(". ids\n+ # 1\n+ # 2\n+ # 3"), {"ids": [1, 2, 3]})

    def test_root_list(self):
        self.assertEqual(ryuu.loads("+ ~ a\n+ ~ b\n+ # 3"), ["a", "b", 3])

    def test_list_of_maps_autosibling(self):
        src = (
            "+\n. appid # 1\n. name ~ Alice\n"
            "+\n. appid # 2\n. name ~ Bob\n"
        )
        self.assertEqual(
            ryuu.loads(src),
            [{"appid": 1, "name": "Alice"}, {"appid": 2, "name": "Bob"}],
        )

    def test_nested_lists(self):
        self.assertEqual(ryuu.loads("+\n+ ~ x\n+ ~ y"), [["x", "y"]])
        self.assertEqual(ryuu.loads(". m\n+\n+\n~ deep\n'\n'"), {"m": [["deep"]]})
        self.assertEqual(
            ryuu.loads(". m\n+\n+\n+\n~ deep\n'\n'\n'"), {"m": [[["deep"]]]}
        )

    def test_map_item_with_nested_map(self):
        src = "+\n. a # 1\n. meta\n. v # 2\n+\n. a # 3\n"
        self.assertEqual(
            ryuu.loads(src),
            [{"a": 1, "meta": {"v": 2}}, {"a": 3}],
        )

    def test_auto_close_lists_before_field(self):
        self.assertEqual(
            ryuu.loads(". tags\n+ ~ a\n. name ~ x"),
            {"tags": ["a"], "name": "x"},
        )
        self.assertEqual(
            ryuu.loads(". tags\n+ ~ a\n. b\n+ ~ c\n. d # 1\n. z # 9"),
            {"tags": ["a"], "b": ["c"], "d": 1, "z": 9},
        )

    def test_childless_field_is_empty_map(self):
        self.assertEqual(ryuu.loads(". a\n'"), {"a": {}})
        self.assertEqual(ryuu.loads(". a"), {"a": {}})

    def test_childless_item_is_dropped(self):
        self.assertEqual(ryuu.loads(". ids\n+\n'"), {"ids": []})
        self.assertEqual(ryuu.loads("+\n'"), [])

    def test_null_and_empty_items(self):
        self.assertEqual(ryuu.loads("+ !\n+ | map\n+ | list"), [None, {}, []])

    def test_empty_containers_inline(self):
        self.assertEqual(
            ryuu.loads(". a | map\n. b | list\n. c !"),
            {"a": {}, "b": [], "c": None},
        )

    def test_lifts(self):
        self.assertEqual(ryuu.loads(". a\n. b # 1\n'. c # 2"), {"a": {"b": 1}, "c": 2})
        self.assertEqual(
            ryuu.loads(". a\n. b # 1\n''"),
            {"a": {"b": 1}},  # второй подъём закрыл корень — файл окончен
        )

    def test_lift_prefix_on_lines(self):
        self.assertEqual(
            ryuu.loads(". a\n. b\n. c # 1\n'. d # 2"),
            {"a": {"b": {"c": 1}, "d": 2}},
        )

    def test_atom_seals_field_and_sibling_continues(self):
        self.assertEqual(
            ryuu.loads(". user\n. name ~ Alice\n. age # 30"),
            {"user": {"name": "Alice", "age": 30}},
        )

    def test_empty_document(self):
        self.assertIsNone(ryuu.loads(""))
        self.assertIsNone(ryuu.loads("\n\n; только комментарий\n"))

    def test_order_preserved(self):
        src = ". z ~ 1\n. a ~ 2\n. m ~ 3"
        self.assertEqual(list(ryuu.loads(src).keys()), ["z", "a", "m"])


class TestBlocksAndComments(unittest.TestCase):
    def test_block_string(self):
        src = ". desc\n~~~\nстрока 1\nстрока 2\n~~~\n"
        self.assertEqual(ryuu.loads(src), {"desc": "строка 1\nстрока 2"})

    def test_empty_block(self):
        self.assertEqual(ryuu.loads(". d\n~~~\n~~~"), {"d": ""})

    def test_block_preserves_spacing_and_comments(self):
        src = ". d\n~~~\n  отступ ; не комментарий\n~~~\n"
        self.assertEqual(ryuu.loads(src), {"d": "  отступ ; не комментарий"})

    def test_block_as_list_item(self):
        src = ". docs\n+\n~~~\nA\n~~~\n+\n~~~\nB\n~~~\n"
        self.assertEqual(ryuu.loads(src), {"docs": ["A", "B"]})

    def test_block_newline_only(self):
        self.assertEqual(ryuu.loads("~~~\n\n\n~~~"), "\n")  # две пустые строки
        self.assertEqual(ryuu.loads("~~~\n~~~"), "")       # пустой блок
        self.assertEqual(ryuu.loads(ryuu.dumps("\n")), "\n")

    def test_unterminated_block(self):
        self.assertTrue(parse_err(". d\n~~~\nabc"))

    def test_comments_and_blanks(self):
        src = "; заголовок\n\n. a # 1\n   ; отступной комментарий\n\n. b # 2\n"
        self.assertEqual(ryuu.loads(src), {"a": 1, "b": 2})

    def test_semicolon_in_string_is_not_comment(self):
        self.assertEqual(ryuu.loads("~ ; не комментарий"), "; не комментарий")


class TestErrors(unittest.TestCase):
    def test_duplicate_field(self):
        self.assertTrue(parse_err(". a # 1\n. a # 2"))

    def test_field_after_scalar_root(self):
        self.assertTrue(parse_err("~ x\n. a # 1"))

    def test_atom_after_scalar_root(self):
        self.assertTrue(parse_err("~ x\n~ y"))

    def test_lift_underflow(self):
        self.assertTrue(parse_err("'. x"))
        self.assertTrue(parse_err(". a ~ 1\n''. b ~ 2"))

    def test_unknown_rune(self):
        self.assertTrue(parse_err("@ x"))
        self.assertTrue(parse_err("hello"))

    def test_field_without_name(self):
        self.assertTrue(parse_err("."))

    def test_trailing_payload_after_name(self):
        self.assertTrue(parse_err(". name garbage"))

    def test_map_field_cannot_become_list(self):
        self.assertTrue(parse_err(". a\n. b # 1\n+ ~ x"))

    def test_closed_list_cannot_reopen(self):
        self.assertTrue(parse_err(". a\n+ ~ 1\n'\n+ ~ 2"))

    def test_field_in_list_is_error(self):
        # '. x' внутри списка без элемента — список закроется сам, но корень
        # уже занят списком => данные после корня
        self.assertTrue(parse_err("+ ~ a\n. x # 1"))

    def test_scalar_into_filled_map(self):
        self.assertTrue(parse_err(". a\n. b # 1\n~ x"))

    def test_payload_after_plus(self):
        self.assertTrue(parse_err("+ garbage"))


class TestRoundTrip(unittest.TestCase):
    CASES = [
        {"user": {"name": "Alice", "age": 30, "pi": 3.14}, "active": True},
        {"a": {"b": {"c": {"d": [1, 2, {"e": []}]}}}},
        [[1, 2], [3, [4, 5]], [], [{"k": "v"}]],
        {"list": ["a", "b"], "map_in_list": [{"x": 1, "y": {"z": 2}}]},
        {"none": None, "t": True, "f": False, "empty_map": {}, "empty_list": []},
        {"n": [0, -1, 1.5, 1e-7, 12345678901234567890]},
        "просто строка",
        42,
        None,
        [],
        {},
        ["", " ", "  ", "символы: . , ~ # ? % & ! $ | ' ; +"],
        {"link": Link("https://example.com/x"), "date": date(2026, 9, 28),
         "dt": datetime(2026, 9, 28, 14, 30, tzinfo=timezone.utc),
         "tm": time(23, 59, 59), "blob": b"\x00\xff\x10hello"},
        {"мультистрок": "первая\nвторая\n\nчетвёртая"},
        {"deep": {"a": [{"b": [{"c": [1, {"d": 2}]}]}]}},
    ]

    def test_roundtrip(self):
        for i, case in enumerate(self.CASES):
            with self.subTest(i=i):
                text = ryuu.dumps(case)
                back = ryuu.loads(text)
                self.assertEqual(back, case)
                # и второй проход — стабильность канонической формы
                self.assertEqual(ryuu.dumps(back), text)

    def test_multi_block_with_marker_fails(self):
        with self.assertRaises(RyuuWriteError):
            ryuu.dumps({"bad": "строка\n~~~\nс маркером"})

    def test_bad_key_space(self):
        with self.assertRaises(RyuuWriteError):
            ryuu.dumps({"плохий ключ": 1})

    def test_bad_key_empty(self):
        with self.assertRaises(RyuuWriteError):
            ryuu.dumps({"": 1})

    def test_bad_key_rune_initial(self):
        with self.assertRaises(RyuuWriteError):
            ryuu.dumps({"~tilde": 1})

    def test_unsupported_type(self):
        with self.assertRaises(RyuuWriteError):
            ryuu.dumps({"x": object()})

    def test_float_formatting(self):
        self.assertEqual(ryuu.loads(ryuu.dumps(1.0)), 1.0)
        self.assertIn("# 1.0", ryuu.dumps(1.0))
        self.assertIn("# inf", ryuu.dumps(float("inf")))
        self.assertTrue(math.isnan(ryuu.loads(ryuu.dumps(float("nan")))))


class TestIO(unittest.TestCase):
    def test_load_dump_file(self):
        obj = {"k": [1, 2, 3], "s": "текст"}
        buf = io.StringIO()
        ryuu.dump(obj, buf)
        buf.seek(0)
        self.assertEqual(ryuu.load(buf), obj)

    def test_crlf_normalized(self):
        self.assertEqual(ryuu.loads(". a # 1\r\n. b ~ 2\r\n"), {"a": 1, "b": "2"})
        self.assertEqual(ryuu.loads(". d\r\n~~~\r\nx\r\n~~~\r\n"), {"d": "x"})

    def test_bom_stripped(self):
        self.assertEqual(ryuu.loads("\ufeff. a # 1"), {"a": 1})

    def test_bytes_input(self):
        self.assertEqual(ryuu.loads(b". a # 1"), {"a": 1})


class TestCLI(unittest.TestCase):
    def _tmp(self, content, suffix):
        fd, path = tempfile.mkstemp(suffix=suffix)
        with os.fdopen(fd, "w", encoding="utf-8") as fp:
            fp.write(content)
        self.addCleanup(os.unlink, path)
        return path

    def test_encode_decode(self):
        obj = {"appid": 2427700, "name": "Ryuu", "tags": ["a", "b"]}
        jpath = self._tmp(json.dumps(obj), ".json")
        rpath = self._tmp("", ".ryu")
        self.assertEqual(cli_main(["encode", jpath, rpath]), 0)
        with open(rpath, encoding="utf-8") as fp:
            self.assertEqual(ryuu.loads(fp.read()), obj)
        out = self._tmp("", ".json")
        self.assertEqual(cli_main(["decode", rpath, out]), 0)
        with open(out, encoding="utf-8") as fp:
            self.assertEqual(json.load(fp), obj)

    def test_check_ok(self):
        rpath = self._tmp(". a # 1\n", ".ryu")
        self.assertEqual(cli_main(["check", rpath]), 0)

    def test_check_bad(self):
        rpath = self._tmp(". a # 1\n. a # 2\n", ".ryu")
        self.assertEqual(cli_main(["check", rpath]), 1)

    def test_decode_special_types(self):
        rpath = self._tmp(
            ". d % 2026-09-14\n. l & https://x.y\n. b $ aGk=\n", ".ryu"
        )
        out = self._tmp("", ".json")
        self.assertEqual(cli_main(["decode", rpath, out]), 0)
        with open(out, encoding="utf-8") as fp:
            data = json.load(fp)
        self.assertEqual(data, {"d": "2026-09-14", "l": "https://x.y", "b": "aGk="})

    def test_encode_stdin_stdout(self):
        from unittest.mock import patch
        buf = io.StringIO()
        with patch("sys.stdin", io.StringIO('{"a": [1, 2]}')):
            import contextlib
            with contextlib.redirect_stdout(buf):
                rc = cli_main(["encode", "-"])
        self.assertEqual(rc, 0)
        self.assertEqual(buf.getvalue(), ". a\n+ # 1\n+ # 2\n")


class TestJSONInterop(unittest.TestCase):
    def test_json_roundtrip(self):
        payload = {
            "appid": 730,
            "name": "Counter-Strike 2",
            "free": False,
            "price": 0.0,
            "tags": ["fps", "action"],
            "meta": {"release": "2012-08-21", "depots": [3736911, 3736912]},
            "empty": {},
            "nothing": None,
        }
        text = ryuu.dumps(payload)
        back = ryuu.loads(text)
        self.assertEqual(json.loads(json.dumps(back)), payload)


class TestFuzzRoundTrip(unittest.TestCase):
    """Псевдослучайные деревья: dumps -> loads обязан давать то же дерево,
    а каноническая форма должна быть стабильной (idempotent)."""

    def test_fuzz(self):
        import random

        rng = random.Random(20260928)

        def atom():
            kind = rng.randrange(8)
            if kind == 0:
                return rng.randrange(-10**12, 10**12)
            if kind == 1:
                return rng.uniform(-1e6, 1e6)
            if kind == 2:
                return rng.choice([True, False])
            if kind == 3:
                return None
            if kind == 4:
                return ""
            if kind == 5:
                return "текст с . , ~ # ? % & ! $ | ' ; + символами"
            if kind == 6:
                return rng.choice(["a", "имя поля", "x" * 30])
            return rng.randrange(0, 10**6) / 8

        def tree(depth):
            if depth <= 0 or rng.random() < 0.55:
                return atom()
            if rng.random() < 0.5:
                return {f"k{rng.randrange(40)}": tree(depth - 1)
                        for _ in range(rng.randrange(0, 5))}
            return [tree(depth - 1) for _ in range(rng.randrange(0, 5))]

        for i in range(300):
            case = tree(rng.randrange(1, 6))
            with self.subTest(i=i):
                text = ryuu.dumps(case)
                back = ryuu.loads(text)
                self.assertEqual(back, case)
                self.assertEqual(ryuu.dumps(back), text)


if __name__ == "__main__":
    unittest.main()
