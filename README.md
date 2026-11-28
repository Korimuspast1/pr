# pr

Два независимых проекта в одном репозитории:

| Проект | Что это |
| --- | --- |
| [`isocity/`](isocity/README.md) | Тестовый город в изометрии без GUI: диорама 20×20 с жителями, работой и замкнутой экономикой (деньги только переходят между агентами, зарплата — из выручки). |
| [`custom-profile/`](custom-profile/README.md) | Installable Elyx profile plugin for exteraGram: local preview of badges, stars and profile colours. |

Тестовый город запускается как статика:

```bash
cd isocity
python3 -m http.server 8080
# http://localhost:8080
```
