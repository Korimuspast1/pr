# Серверная проверка БЕЗ Cloud Functions — правила Firestore

**Как это работает без Cloud Functions:** правила ниже выполняются **на серверах Google**
при каждом запросе к Firestore. Приложение (и любой подделанный клиент) физически не может
записать данные, которые правила запрещают: нельзя выдать себе подарок через P2P, нельзя
принять чужую передачу, нельзя купить свой лот, нельзя поднять статус заявки в друзья самому
себе, нельзя изменить чужой документ. Это и есть серверная проверка — отдельный серверный
код для неё не нужен.

**Честное ограничение:** правила не могут проверить то, чего нет в структурированных полях.
Баланс и инвентарь внутри `users.stateJson` — это одна строка, правила не умеют её разбирать,
поэтому для сейва действуют лимиты (размер, типы, потолок роста баланса за одну запись).
Полная проверка игровой экономики возможна только при переносе инвентаря в отдельные
документы — это отдельная миграция.

---

## Куда вставлять (прямые ссылки, проект `lool-4d49c`)

1. **Включить вход Email/Password** (если ещё не включён):
   https://console.firebase.google.com/project/lool-4d49c/authentication/providers
   → Email/Password → Enable → Save. *(В приложении вводится только логин+пароль, почта не нужна.)*

2. **Создать базу Firestore** (если ещё не создана):
   https://console.firebase.google.com/project/lool-4d49c/firestore
   → Create database → регион `eur3` → Create.

3. **Вставить правила — прямая ссылка на редактор правил:**
   https://console.firebase.google.com/project/lool-4d49c/firestore/databases/-default-/rules
   → полностью удалить старый текст → вставить правила ниже → **Publish** (Опубликовать).

Всё. Пересобирать APK после публикации правил не нужно — приложение подхватит их сразу.

---

## Правила (копировать целиком)

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    function signedIn() { return request.auth != null; }
    function me(uid) { return signedIn() && request.auth.uid == uid; }

    // ---------- Сейв игрока: только свой, строгие типы и лимиты ----------
    function validSave(d) {
      return d.keys().hasOnly(['login', 'coins', 'itemsCount', 'dexCount', 'updatedAt', 'stateJson'])
        && d.login is string && d.login.size() <= 64
        && d.coins is int && d.coins >= 0 && d.coins <= 100000000
        && d.itemsCount is int && d.itemsCount >= 0 && d.itemsCount <= 100000
        && d.dexCount is int && d.dexCount >= 0 && d.dexCount <= 100000
        && d.updatedAt is int && d.updatedAt > 0
        && d.stateJson is string && d.stateJson.size() < 950000;
    }
    match /users/{userId} {
      // Читать свой сейв может владелец; любой сейв — только админ @visa (список игроков в админке)
      allow read: if me(userId) || isAdmin();
      allow create: if (me(userId) && validSave(request.resource.data))
        || (isAdmin() && validAdminSave(request.resource.data));
      // Баланс за одну запись не может вырасти больше чем на 1 000 000 — массовая накрутка одним запросом закрыта.
      // Админ (@visa) пишет без этого потолка — выдача из админ-панели в приложении.
      allow update: if (me(userId) && validSave(request.resource.data)
          && request.resource.data.coins <= resource.data.coins + 1000000)
        || (isAdmin() && validAdminSave(request.resource.data));
      allow delete: if isAdmin();
    }

    // ---------- Публичные профили соцсети ----------
    function validPub(d, userId) {
      return d.keys().hasOnly(['uid', 'login', 'username', 'nick', 'bio', 'coins', 'itemsCount', 'dexCount', 'giftsJson', 'updatedAt', 'pubKey'])
        && d.uid == userId
        && d.login is string && d.login.size() <= 64
        && d.nick is string && d.nick.size() <= 64
        && d.coins is int && d.coins >= 0
        && d.giftsJson is string && d.giftsJson.size() <= 4000
        && (!('pubKey' in d) || (d.pubKey is string && d.pubKey.size() <= 512));
    }
    match /pub/{userId} {
      allow read: if signedIn();
      allow create, update: if me(userId) && validPub(request.resource.data, userId);
      allow delete: if false;
    }

    // ---------- Заявки в друзья: создать может только отправитель со статусом pending,
    // принять (pending -> ok) может только получатель, менять можно только поле status ----------
    match /freq/{docId} {
      allow read: if signedIn()
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow create: if signedIn()
        && request.resource.data.keys().hasOnly(['from', 'to', 'fromLogin', 'toLogin', 'status', 'ts'])
        && request.resource.data.from == request.auth.uid
        && request.resource.data.to != request.auth.uid
        && request.resource.data.status == 'pending';
      allow update: if signedIn()
        && resource.data.to == request.auth.uid
        && resource.data.status == 'pending'
        && request.resource.data.status == 'ok'
        && request.resource.data.diff(resource.data).affectedKeys().hasOnly(['status']);
      allow delete: if signedIn()
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
    }

    // ---------- Личные сообщения: только создать от своего имени, читать — только участники.
    // Изменить или удалить сообщение нельзя никому. Текст зашифрован E2E на устройстве. ----------
    match /dm/{docId} {
      allow read: if signedIn()
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow create: if signedIn()
        && request.resource.data.keys().hasOnly(['from', 'to', 'fromLogin', 'toLogin', 'text', 'enc', 'ts'])
        && request.resource.data.from == request.auth.uid
        && request.resource.data.to != request.auth.uid
        && request.resource.data.text is string && request.resource.data.text.size() <= 12000
        && request.resource.data.enc is int
        && (request.resource.data.enc == 0 || request.resource.data.enc == 1);
      allow update, delete: if false;
    }

    // ---------- Передачи подарков: машина состояний на сервере ----------
    // pending -> accepted/rejected решает только получатель; вернуть rejected себе может только отправитель.
    match /transfers/{docId} {
      allow read: if signedIn()
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow create: if signedIn()
        && request.resource.data.keys().hasOnly(['from', 'to', 'fromLogin', 'toLogin', 'giftJson', 'status', 'claimed', 'ts'])
        && request.resource.data.from == request.auth.uid
        && request.resource.data.to != request.auth.uid
        && request.resource.data.status == 'pending'
        && request.resource.data.claimed == 0
        && request.resource.data.giftJson is string && request.resource.data.giftJson.size() <= 2000;
      allow update: if signedIn()
        && request.resource.data.diff(resource.data).affectedKeys().hasOnly(['status', 'claimed'])
        && (
          (resource.data.to == request.auth.uid && resource.data.status == 'pending'
            && ((request.resource.data.status == 'accepted' && request.resource.data.claimed == 1)
             || (request.resource.data.status == 'rejected' && request.resource.data.claimed == 0)))
          || (resource.data.from == request.auth.uid && resource.data.status == 'rejected'
            && resource.data.claimed == 0 && request.resource.data.status == 'rejected'
            && request.resource.data.claimed == 1)
        );
      allow delete: if false;
    }

    // ---------- P2P-маркет: машина состояний на сервере ----------
    // open -> sold фиксирует только покупатель (и только на себя); забрать оплату — только продавец;
    // снять лот (delete) — только продавец и только пока open. Купить свой лот нельзя.
    match /p2p/{docId} {
      allow read: if signedIn();
      allow create: if signedIn()
        && request.resource.data.keys().hasOnly(['seller', 'sellerLogin', 'buyer', 'buyerLogin', 'giftJson', 'price', 'status', 'claimed', 'ts'])
        && request.resource.data.seller == request.auth.uid
        && request.resource.data.status == 'open'
        && request.resource.data.claimed == 0
        && request.resource.data.buyer == ''
        && request.resource.data.price is int
        && request.resource.data.price >= 1 && request.resource.data.price <= 100000000
        && request.resource.data.giftJson is string && request.resource.data.giftJson.size() <= 2000;
      allow update: if signedIn()
        && request.resource.data.diff(resource.data).affectedKeys().hasOnly(['status', 'buyer', 'buyerLogin', 'claimed'])
        && (
          (resource.data.status == 'open' && resource.data.seller != request.auth.uid
            && request.resource.data.status == 'sold' && request.resource.data.buyer == request.auth.uid)
          || (resource.data.seller == request.auth.uid && resource.data.status == 'sold'
            && resource.data.claimed == 0 && request.resource.data.status == 'sold'
            && request.resource.data.claimed == 1)
        );
      allow delete: if signedIn()
        && resource.data.seller == request.auth.uid && resource.data.status == 'open';
    }

    // ---------- Всё остальное закрыто ----------
    match /{document=**} {
      allow read, write: if false;
    }
  }
}
```

## Как проверить, что правила встали

1. Открой приложение, войди в аккаунт — люди/соцхаб должны загрузиться (раньше без правил был пустой экран соцхаба и ошибки синхронизации).
2. В редакторе правил есть вкладка **Rules Playground / Симулятор**: запрос от чужого `uid` на `p2p/{лот}` со сменой `claimed` должен получить **Denied**.
