# Исходники образца fakevpn.zip

> **Внимание.** Это автоматический разбор вредоносного кода.
> Полные java/smali-исходники лежат в артефакте Actions
> `malware-sources-do-not-run` (хранится 7 дней) и намеренно не
> коммитятся: это работоспособный банковский троян.
> Здесь только манифест, ресурсы и карта классов — без тела методов.

## Button_VPN

```
APK: Button VPN.apk
sha256: b44d0b07913b5512f12af4044af0f043be9f824a67d7457e00a898d9f178a369

== пакет и разрешения ==
package="com.buttonvpn.service.v1ecdcd69"
android:name="android.permission.INTERNET"
android:name="android.permission.READ_EXTERNAL_STORAGE"
android:name="android.permission.REQUEST_INSTALL_PACKAGES"
android:name="android.permission.WRITE_EXTERNAL_STORAGE"

== статистика декомпиляции ==
java-файлов: 30
smali-файлов: 29
строк Java: 1285
```

- `Button_VPN/AndroidManifest.xml`, `Button_VPN/res/`, `Button_VPN/class-map.txt`, `Button_VPN/api-usage.txt`

## Button_VPN_1_

```
APK: Button VPN(1).apk
sha256: c3a5895b6d38af17da610c1e66d20888dc10b5c94bebf30d5c7acefa3c92ad85

== пакет и разрешения ==
package="com.buttonvpn.hjexqm"
android:name="android.permission.ACCESS_NETWORK_STATE"
android:name="android.permission.CAMERA"
android:name="android.permission.FOREGROUND_SERVICE"
android:name="android.permission.FOREGROUND_SERVICE_DATA_SYNC"
android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK"
android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PROJECTION"
android:name="android.permission.FOREGROUND_SERVICE_SPECIAL_USE"
android:name="android.permission.INTERNET"
android:name="android.permission.MANAGE_EXTERNAL_STORAGE"
android:name="android.permission.POST_NOTIFICATIONS"
android:name="android.permission.QUERY_ALL_PACKAGES"
android:name="android.permission.READ_CELL_BROADCASTS"
android:name="android.permission.READ_CONTACTS"
android:name="android.permission.READ_EXTERNAL_STORAGE"
android:name="android.permission.READ_PHONE_NUMBERS"
android:name="android.permission.READ_PHONE_STATE"
android:name="android.permission.READ_SMS"
android:name="android.permission.RECEIVE_BOOT_COMPLETED"
android:name="android.permission.RECEIVE_MMS"
android:name="android.permission.RECEIVE_SMS"
android:name="android.permission.RECEIVE_WAP_PUSH"
android:name="android.permission.RECORD_AUDIO"
android:name="android.permission.REQUEST_DELETE_PACKAGES"
android:name="android.permission.REQUEST_IGNORE_BATTERY_OPTIMIZATIONS"
android:name="android.permission.SEND_SMS"
android:name="android.permission.TURN_SCREEN_ON"
android:name="android.permission.USE_EXACT_ALARM"
android:name="android.permission.WAKE_LOCK"
android:name="android.permission.WRITE_EXTERNAL_STORAGE"
android:name="android.permission.WRITE_SMS"
android:name="android.permission.WRITE_SYNC_SETTINGS"

== статистика декомпиляции ==
java-файлов: 577
smali-файлов: 665
строк Java: 37893
```

- `Button_VPN_1_/AndroidManifest.xml`, `Button_VPN_1_/res/`, `Button_VPN_1_/class-map.txt`, `Button_VPN_1_/api-usage.txt`

Полный архив кода: Actions → *Dump sources of the sample* → Artifacts → `malware-sources-do-not-run`.
