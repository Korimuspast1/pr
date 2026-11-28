# Настройка Firebase (`lool-4d49c`) и Правила безопасности

Приложение сконфигурировано под ваш `google-services.json`:
- **Project ID:** `lool-4d49c`
- **Package Name:** `cocaser.app`
- **App ID:** `1:76010286017:android:658dd9032a847a58931d40`

---

## Шаг 1. Включить вход по логину и паролю
1. Откройте [Firebase Console](https://console.firebase.google.com/) → проект **`lool-4d49c`**.
2. Перейдите в **Build → Authentication → Sign-in method** (Способы входа).
3. Нажмите **Email/Password** → включите верхний переключатель **Enable** → нажмите **Save**.
   *(В самом приложении вводить почту НЕ нужно — игрок вводит только простой **Логин** и **Пароль**).*

---

## Шаг 2. Создать Cloud Firestore и вставить Правила (Rules)
1. В левом меню откройте **Build → Firestore Database**.
2. Если база ещё не создана, нажмите **Create database** → выберите ближайший регион (например, `eur3`) → **Create**.
3. Перейдите на вкладку **Rules** (Правила), полностью замените текст на правила ниже и нажмите **Publish** (Опубликовать):

```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    // Игрок может читать и сохранять ТОЛЬКО свой собственный профиль и инвентарь
    match /users/{userId} {
      allow read: if request.auth != null && request.auth.uid == userId;
      allow create, update: if request.auth != null
        && request.auth.uid == userId
        && request.resource.data.keys().hasOnly([
          'login',
          'coins',
          'itemsCount',
          'dexCount',
          'updatedAt',
          'stateJson'
        ])
        && request.resource.data.stateJson is string
        && request.resource.data.stateJson.size() < 950000;
      allow delete: if false;
    }

    // === Социальные функции (v2.2): публичные профили ===
    match /pub/{userId} {
      allow read: if request.auth != null;
      allow create, update: if request.auth != null && request.auth.uid == userId;
      allow delete: if false;
    }

    // Заявки в друзья: видны и меняются только участниками
    match /freq/{docId} {
      allow read: if request.auth != null
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow create: if request.auth != null && request.resource.data.from == request.auth.uid;
      allow update: if request.auth != null
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow delete: if request.auth != null
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
    }

    // Личные сообщения (текст зашифрован E2E на устройстве): только участники переписки
    match /dm/{docId} {
      allow read: if request.auth != null
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow create: if request.auth != null && request.resource.data.from == request.auth.uid;
      allow update, delete: if false;
    }

    // Передачи подарков между игроками: только отправитель и получатель
    match /transfers/{docId} {
      allow read: if request.auth != null
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow create: if request.auth != null && request.resource.data.from == request.auth.uid;
      allow update: if request.auth != null
        && (resource.data.from == request.auth.uid || resource.data.to == request.auth.uid);
      allow delete: if false;
    }

    // P2P-маркет: лоты видны всем вошедшим, создаёт только продавец
    match /p2p/{docId} {
      allow read: if request.auth != null;
      allow create: if request.auth != null && request.resource.data.seller == request.auth.uid;
      allow update: if request.auth != null;
      allow delete: if request.auth != null && resource.data.seller == request.auth.uid;
    }

    // Все остальные пути закрыты
    match /{document=**} {
      allow read, write: if false;
    }
  }
}
```

---

## (Опционально) Правила для Realtime Database, если она также включена в проекте
Основная синхронизация работает через **Firestore Database** выше. Если у вас параллельно создана **Realtime Database**, поставьте в её вкладке **Rules** следующие закрытые персональные правила:

```json
{
  "rules": {
    "users": {
      "$uid": {
        ".read": "auth != null && auth.uid === $uid",
        ".write": "auth != null && auth.uid === $uid"
      }
    }
  }
}
```
