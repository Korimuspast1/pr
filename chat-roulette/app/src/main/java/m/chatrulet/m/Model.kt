package m.chatrulet.m

import android.app.Application
import android.content.Context
import android.media.AudioManager
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.google.firebase.Timestamp
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.firestore.AggregateSource
import com.google.firebase.firestore.DocumentReference
import com.google.firebase.firestore.DocumentSnapshot
import com.google.firebase.firestore.FieldValue
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.ListenerRegistration
import com.google.firebase.firestore.Query
import com.google.firebase.firestore.SetOptions
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.tasks.await
import org.json.JSONObject
import org.webrtc.DataChannel
import org.webrtc.IceCandidate
import org.webrtc.MediaConstraints
import org.webrtc.SessionDescription
import org.webrtc.VideoTrack
import java.nio.ByteBuffer
import java.security.KeyPair
import java.util.Date
import javax.crypto.spec.SecretKeySpec

enum class Phase { IDLE, SEARCHING, CONNECTING, IN_CALL }
enum class Screen { HOME, SETTINGS, RULES, BLOCKED }

data class ChatMsg(val text: String, val mine: Boolean, val ts: Long = System.currentTimeMillis())

data class PeerInfo(
    val uid: String = "",
    val nick: String = "",
    val country: String = "",
    val lang: String = "",
    val gender: String = "",
    val interests: List<String> = emptyList(),
)

class AppViewModel(app: Application) : AndroidViewModel(app) {

    private val auth = FirebaseAuth.getInstance()
    private val db = FirebaseFirestore.getInstance()
    private val audio = app.getSystemService(Context.AUDIO_SERVICE) as AudioManager

    val engine = RtcEngine(app)

    // ── наблюдаемое состояние ──
    var uid by mutableStateOf("")
        private set
    var online by mutableLongStateOf(-1L)
        private set
    var phase by mutableStateOf(Phase.IDLE)
        private set
    var screen by mutableStateOf(Screen.HOME)
    var peer by mutableStateOf(PeerInfo())
        private set
    val messages = mutableStateListOf<ChatMsg>()
    var peerTyping by mutableStateOf(false)
        private set
    var callSeconds by mutableIntStateOf(0)
        private set
    var quality by mutableIntStateOf(0)          // 0 нет · 1 плохо · 2 норм · 3 отлично
        private set
    var remoteTrack by mutableStateOf<VideoTrack?>(null)
    var remoteHasVideo by mutableStateOf(false)
        private set
    var peerCamOn by mutableStateOf(true)
        private set
    var chatOpen by mutableStateOf(false)
    var unread by mutableIntStateOf(0)
        private set
    var reaction by mutableStateOf<String?>(null)
    var toast by mutableStateOf<String?>(null)
    var callId by mutableIntStateOf(0)
        private set

    // ── внутреннее ──
    private var roomRef: DocumentReference? = null
    private var roomId: String? = null
    private var isCaller = false
    private var myQueueRef: DocumentReference? = null
    private var roomListener: ListenerRegistration? = null
    private var incomingListener: ListenerRegistration? = null
    private var iceListener: ListenerRegistration? = null
    private var dc: DataChannel? = null
    private var dcOpen = false
    private var keyPair: KeyPair? = null
    private var peerPub: String? = null
    private var sessionKey: SecretKeySpec? = null
    private var expectEnd = false
    private var offerApplied = false
    private var answerApplied = false
    private var connectedOnce = false
    private var callTimer: Job? = null
    private var searchJob: Job? = null
    private var queueRefreshJob: Job? = null
    private var heartbeatJob: Job? = null
    private var timeoutJob: Job? = null
    private var typingJob: Job? = null
    private var lastTypingSent = 0L

    init {
        engine.events = object : RtcEngine.Events {
            override fun onIce(candidate: IceCandidate) = sendIce(candidate)
            override fun onConnected() = onRtcConnected()
            override fun onDisconnected(reason: String) = onRtcDisconnected(reason)
            override fun onDataChannel(ch: DataChannel) = registerDc(ch)
            override fun onRemoteVideo(track: VideoTrack) {
                remoteTrack = track
                remoteHasVideo = true
            }
        }
        signIn()
        startHeartbeat()
    }

    // ═════════════════════════════════════════════ вход и присутствие

    fun signIn() {
        viewModelScope.launch {
            try {
                if (auth.currentUser == null) auth.signInAnonymously().await()
                uid = auth.uid ?: ""
                listenIncoming()
                refreshOnline()
            } catch (e: Throwable) {
                toast = tr("No connection to server", "Нет соединения с сервером")
            }
        }
    }

    private fun startHeartbeat() {
        heartbeatJob?.cancel()
        heartbeatJob = viewModelScope.launch {
            while (isActive) {
                try {
                    if (uid.isNotEmpty()) {
                        db.collection("users").document(uid).set(
                            mapOf(
                                "lastSeen" to FieldValue.serverTimestamp(),
                                "country" to Store.myCountry(),
                                "lang" to Store.myLang(),
                            ),
                            SetOptions.merge(),
                        ).await()
                    }
                    refreshOnline()
                } catch (e: Throwable) {
                }
                delay(30_000)
            }
        }
    }

    private suspend fun refreshOnline() {
        try {
            val cutoff = Timestamp(Date(System.currentTimeMillis() - 120_000))
            val res = db.collection("users")
                .whereGreaterThan("lastSeen", cutoff)
                .count()
                .get(AggregateSource.SERVER)
                .await()
            online = res.count
        } catch (e: Throwable) {
        }
    }

    // ═════════════════════════════════════════════ поиск собеседника

    fun startSearch() {
        if (uid.isEmpty()) {
            signIn()
            toast = tr("Connecting to server…", "Подключаемся к серверу…")
            return
        }
        if (phase == Phase.SEARCHING || phase == Phase.CONNECTING || phase == Phase.IN_CALL) return
        cleanupCall()
        phase = Phase.SEARCHING
        keyPair = E2E.keyPair()
        viewModelScope.launch {
            try {
                val ref = db.collection("queue").document(uid)
                myQueueRef = ref
                ref.set(queuePayload()).await()
            } catch (e: Throwable) {
                phase = Phase.IDLE
                toast = tr("Search failed — check Firestore rules", "Поиск не удался — проверь правила Firestore")
                return@launch
            }
        }
        // освежаем метку времени, чтобы запись не считалась «мёртвой»
        queueRefreshJob = viewModelScope.launch {
            while (isActive && phase == Phase.SEARCHING) {
                delay(12_000)
                try {
                    myQueueRef?.update("ts", FieldValue.serverTimestamp())?.await()
                } catch (e: Throwable) {
                }
            }
        }
        searchJob = viewModelScope.launch {
            while (isActive && phase == Phase.SEARCHING) {
                try {
                    val cutoff = Timestamp(Date(System.currentTimeMillis() - 90_000))
                    val qs = db.collection("queue")
                        .whereGreaterThan("ts", cutoff)
                        .orderBy("ts", Query.Direction.ASCENDING)
                        .limit(40)
                        .get()
                        .await()
                    val mine = Store.interestsSet()
                    val cand = qs.documents
                        .filter { matches(it) }
                        .maxByOrNull { overlap(it) }
                    if (cand != null && tryMatch(cand)) break
                    delay(1500)
                } catch (e: Throwable) {
                    delay(2200)
                }
            }
        }
    }

    fun cancelSearch() {
        if (phase != Phase.SEARCHING) return
        phase = Phase.IDLE
        searchJob?.cancel()
        queueRefreshJob?.cancel()
        val ref = myQueueRef
        myQueueRef = null
        viewModelScope.launch {
            try {
                ref?.delete()?.await()
            } catch (e: Throwable) {
            }
        }
    }

    private fun queuePayload(): Map<String, Any> = mapOf(
        "status" to "waiting",
        "uid" to uid,
        "ts" to FieldValue.serverTimestamp(),
        "country" to Store.myCountry(),
        "lang" to Store.myLang(),
        "gender" to Store.gender,
        "interests" to Store.interestsSet().toList(),
        "blocks" to Store.blocked.map { it.uid },
        "pub" to E2E.pubB64(keyPair!!),
        "nick" to Store.nick,
    )

    @Suppress("UNCHECKED_CAST")
    private fun matches(d: DocumentSnapshot): Boolean {
        val theirUid = d.getString("uid") ?: d.id
        if (theirUid == uid) return false
        if (d.getString("status") != "waiting") return false
        if (Store.isBlocked(theirUid)) return false
        val theirBlocks = d.get("blocks") as? List<String> ?: emptyList()
        if (theirBlocks.contains(uid)) return false
        if (Store.countryFilter.isNotBlank() && d.getString("country") != Store.countryFilter) return false
        if (Store.genderFilter.isNotBlank() && d.getString("gender") != Store.genderFilter) return false
        if (Store.langFilter.isNotBlank() && d.getString("lang") != Store.langFilter) return false
        return true
    }

    @Suppress("UNCHECKED_CAST")
    private fun overlap(d: DocumentSnapshot): Int {
        val theirs = (d.get("interests") as? List<String> ?: emptyList()).toSet()
        return Store.interestsSet().intersect(theirs).size
    }

    // ═════════════════════════════════════════════ создание комнаты (звонящий)

    private suspend fun tryMatch(cand: DocumentSnapshot): Boolean {
        val ref = roomRefNew()
        val kp = keyPair ?: return false
        val calleeMeta = mapOf(
            "nick" to (cand.getString("nick") ?: ""),
            "country" to (cand.getString("country") ?: ""),
            "lang" to (cand.getString("lang") ?: ""),
            "gender" to (cand.getString("gender") ?: ""),
            "interests" to (cand.get("interests") as? List<String> ?: emptyList<String>()),
        )
        val callerMeta = mapOf(
            "nick" to Store.nick,
            "country" to Store.myCountry(),
            "lang" to Store.myLang(),
            "gender" to Store.gender,
            "interests" to Store.interestsSet().toList(),
        )
        val room = hashMapOf<String, Any?>(
            "caller" to uid,
            "callee" to (cand.getString("uid") ?: cand.id),
            "callerKey" to E2E.pubB64(kp),
            "calleeKey" to cand.getString("pub"),
            "callerMeta" to callerMeta,
            "calleeMeta" to calleeMeta,
            "status" to "connecting",
            "createdAt" to FieldValue.serverTimestamp(),
        )
        return try {
            val qref = myQueueRef
            db.runTransaction<Void?> { tx ->
                val snap = tx.get(cand.reference)
                if (snap.getString("status") != "waiting") {
                    throw com.google.firebase.firestore.FirebaseFirestoreException(
                        "taken", com.google.firebase.firestore.FirebaseFirestoreException.Code.ABORTED,
                    )
                }
                tx.set(ref, room)
                if (qref != null) tx.delete(qref)
                null
            }.await()
            myQueueRef = null
            becomeCaller(ref, calleeMeta, cand.getString("pub"))
            true
        } catch (e: Throwable) {
            false
        }
    }

    private fun roomRefNew(): DocumentReference = db.collection("rooms").document()

    @Suppress("UNCHECKED_CAST")
    private fun becomeCaller(ref: DocumentReference, calleeMeta: Map<String, Any?>, calleePub: String?) {
        searchJob?.cancel()
        queueRefreshJob?.cancel()
        isCaller = true
        roomRef = ref
        roomId = ref.id
        peerPub = calleePub
        peer = PeerInfo(
            uid = "", // заполним из слушателя комнаты, uid собеседника = callee
            nick = calleeMeta["nick"] as? String ?: "",
            country = calleeMeta["country"] as? String ?: "",
            lang = calleeMeta["lang"] as? String ?: "",
            gender = calleeMeta["gender"] as? String ?: "",
            interests = (calleeMeta["interests"] as? List<String>) ?: emptyList(),
        )
        phase = Phase.CONNECTING
        callId++
        startCallTimers()
        setupPeerAsCaller(ref)
        watchRoom(ref)
        armTimeout(ref)
        listenIce()
    }

    private fun setupPeerAsCaller(ref: DocumentReference) {
        engine.ensureStarted()
        engine.resumeCapture()
        engine.setBlur(Store.blurCam)
        val pc = engine.createPeer() ?: run {
            toast = tr("WebRTC is unavailable on this device", "WebRTC недоступен на этом устройстве")
            stop(); return
        }
        val ch = pc.createDataChannel("chat", DataChannel.Init())
        dc = ch
        registerDc(ch)
        val mc = MediaConstraints().apply {
            mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveAudio", "true"))
            mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveVideo", "true"))
        }
        pc.createOffer(object : SimpleSdp() {
            override fun onCreateSuccess(sdp: SessionDescription?) {
                if (sdp == null) return
                pc.setLocalDescription(SimpleSdp(), sdp)
                ref.update(
                    mapOf("offer" to mapOf("type" to sdp.type.canonicalForm(), "sdp" to sdp.description)),
                )
            }
        }, mc)
    }

    // ═════════════════════════════════════════════ входящий звонок (вызываемый)

    private fun listenIncoming() {
        incomingListener?.remove()
        incomingListener = db.collection("rooms")
            .whereEqualTo("callee", uid)
            .addSnapshotListener { snap, _ ->
                if (snap == null) return@addSnapshotListener
                if (phase != Phase.SEARCHING) return@addSnapshotListener // заняты — звонящий отвалится по таймауту
                val doc = snap.documents
                    .filter { it.getString("status") == "connecting" }
                    .maxByOrNull { it.getTimestamp("createdAt")?.seconds ?: 0L }
                    ?: return@addSnapshotListener
                acceptRoom(doc)
            }
    }

    @Suppress("UNCHECKED_CAST")
    private fun acceptRoom(doc: DocumentSnapshot) {
        // выходим из очереди — нас нашли
        searchJob?.cancel()
        queueRefreshJob?.cancel()
        val qref = myQueueRef
        myQueueRef = null
        if (qref != null) {
            viewModelScope.launch { try { qref.delete().await() } catch (e: Throwable) {} }
        }
        isCaller = false
        roomRef = doc.reference
        roomId = doc.id
        peerPub = doc.getString("callerKey")
        val meta = doc.get("callerMeta") as? Map<String, Any?> ?: emptyMap()
        peer = PeerInfo(
            uid = doc.getString("caller") ?: "",
            nick = meta["nick"] as? String ?: "",
            country = meta["country"] as? String ?: "",
            lang = meta["lang"] as? String ?: "",
            gender = meta["gender"] as? String ?: "",
            interests = (meta["interests"] as? List<String>) ?: emptyList(),
        )
        if (keyPair == null) keyPair = E2E.keyPair()
        phase = Phase.CONNECTING
        callId++
        startCallTimers()
        engine.ensureStarted()
        engine.resumeCapture()
        engine.setBlur(Store.blurCam)
        if (engine.createPeer() == null) {
            toast = tr("WebRTC is unavailable on this device", "WebRTC недоступен на этом устройстве")
            stop()
            return
        }
        watchRoom(doc.reference)
        armTimeout(doc.reference)
        listenIce()
    }

    // ═════════════════════════════════════════════ слушатель комнаты + SDP

    @Suppress("UNCHECKED_CAST")
    private fun watchRoom(ref: DocumentReference) {
        roomListener?.remove()
        roomListener = ref.addSnapshotListener { snap, e ->
            if (e != null || snap == null) return@addSnapshotListener
            if (!snap.exists()) {
                onPeerEnded()
                return@addSnapshotListener
            }
            if (snap.getString("status") == "ended") {
                onPeerEnded()
                return@addSnapshotListener
            }
            if (isCaller) {
                peer = peer.copy(uid = snap.getString("callee") ?: peer.uid)
                if (!answerApplied) {
                    val ans = snap.get("answer") as? Map<String, Any?>
                    val sdp = ans?.get("sdp") as? String
                    if (sdp != null) {
                        answerApplied = true
                        engine.pc?.setRemoteDescription(
                            SimpleSdp(),
                            SessionDescription(SessionDescription.Type.ANSWER, sdp),
                        )
                        listenIce()
                    }
                }
            } else {
                if (!offerApplied) {
                    val off = snap.get("offer") as? Map<String, Any?>
                    val sdp = off?.get("sdp") as? String
                    if (sdp != null) {
                        offerApplied = true
                        applyOffer(sdp)
                    }
                }
            }
        }
    }

    private fun applyOffer(sdp: String) {
        val pc = engine.pc ?: return
        pc.setRemoteDescription(object : SimpleSdp() {
            override fun onSetSuccess() {
                val mc = MediaConstraints().apply {
                    mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveAudio", "true"))
                    mandatory.add(MediaConstraints.KeyValuePair("OfferToReceiveVideo", "true"))
                }
                pc.createAnswer(object : SimpleSdp() {
                    override fun onCreateSuccess(answer: SessionDescription?) {
                        if (answer == null) return
                        pc.setLocalDescription(SimpleSdp(), answer)
                        roomRef?.update(
                            mapOf("answer" to mapOf("type" to answer.type.canonicalForm(), "sdp" to answer.description)),
                        )
                    }
                }, mc)
            }
        }, SessionDescription(SessionDescription.Type.OFFER, sdp))
    }

    // ═════════════════════════════════════════════ ICE

    private fun sendIce(c: IceCandidate) {
        val ref = roomRef ?: return
        val sub = if (isCaller) "callerIce" else "calleeIce"
        ref.collection(sub).add(
            mapOf(
                "sdpMid" to c.sdpMid,
                "idx" to c.sdpMLineIndex,
                "sdp" to c.sdp,
                "ts" to FieldValue.serverTimestamp(),
            ),
        )
    }

    private fun listenIce() {
        val ref = roomRef ?: return
        iceListener?.remove()
        val sub = if (isCaller) "calleeIce" else "callerIce"
        iceListener = ref.collection(sub).addSnapshotListener { snap, _ ->
            if (snap == null) return@addSnapshotListener
            for (ch in snap.documentChanges) {
                if (ch.type != com.google.firebase.firestore.DocumentChange.Type.ADDED) continue
                val d = ch.document
                val sdp = d.getString("sdp") ?: continue
                engine.pc?.addIceCandidate(
                    IceCandidate(d.getString("sdpMid"), (d.getLong("idx") ?: 0L).toInt(), sdp),
                )
            }
        }
    }

    // ═════════════════════════════════════════════ жизненный цикл звонка

    private fun armTimeout(ref: DocumentReference) {
        val id = ref.id
        timeoutJob?.cancel()
        timeoutJob = viewModelScope.launch {
            delay(18_000)
            if (phase == Phase.CONNECTING && roomId == id) {
                // собеседник не ответил / соединение не поднялось
                expectEnd = true
                try { ref.delete().await() } catch (e: Throwable) {}
                cleanupCall()
                startSearch()
            }
        }
    }

    private fun startCallTimers() {
        callTimer?.cancel()
        callTimer = viewModelScope.launch {
            var tick = 0
            while (isActive && (phase == Phase.CONNECTING || phase == Phase.IN_CALL)) {
                delay(1000)
                if (phase == Phase.IN_CALL) {
                    callSeconds++
                    tick++
                    if (tick % 2 == 0) {
                        engine.pollRtt { rtt ->
                            quality = when {
                                rtt < 0 -> quality
                                rtt < 150 -> 3
                                rtt < 400 -> 2
                                else -> 1
                            }
                        }
                    }
                    if (Store.autoNext > 0 && callSeconds >= Store.autoNext) {
                        next()
                        return@launch
                    }
                }
            }
        }
    }

    private fun onRtcConnected() {
        if (phase != Phase.CONNECTING && phase != Phase.IN_CALL) return
        timeoutJob?.cancel()
        if (!connectedOnce) {
            connectedOnce = true
            messages.clear()
            unread = 0
            // ключ шифрования сессии
            val kp = keyPair
            val pub = peerPub
            if (kp != null && pub != null) {
                sessionKey = try { E2E.derive(kp.private, pub) } catch (e: Throwable) { null }
            }
            // динамик по умолчанию в видеочате
            try {
                audio.isSpeakerphoneOn = Store.speakerOn
            } catch (e: Throwable) {
            }
            if (isCaller) {
                viewModelScope.launch {
                    try { roomRef?.update("status", "active")?.await() } catch (e: Throwable) {}
                }
            }
        }
        phase = Phase.IN_CALL
    }

    private fun onRtcDisconnected(reason: String) {
        if (expectEnd) return
        if (phase == Phase.IN_CALL || phase == Phase.CONNECTING) {
            toast = tr("Connection lost", "Соединение потеряно")
            expectEnd = true
            cleanupCall()
            startSearch()
        }
    }

    private fun onPeerEnded() {
        if (expectEnd) return
        expectEnd = true
        toast = tr("Your partner left the chat", "Собеседник завершил чат")
        cleanupCall()
        startSearch()
    }

    /** «Далее»: завершить текущий звонок и сразу искать нового. */
    fun next() {
        if (phase != Phase.IN_CALL && phase != Phase.CONNECTING) return
        expectEnd = true
        endRoomRemote()
        cleanupCall()
        startSearch()
    }

    /** «Стоп»: завершить и выйти на главный экран. */
    fun stop() {
        expectEnd = true
        endRoomRemote()
        cancelSearchSilent()
        cleanupCall()
        phase = Phase.IDLE
        screen = Screen.HOME
    }

    private fun cancelSearchSilent() {
        searchJob?.cancel()
        queueRefreshJob?.cancel()
        val ref = myQueueRef
        myQueueRef = null
        if (ref != null) {
            viewModelScope.launch { try { ref.delete().await() } catch (e: Throwable) {} }
        }
    }

    private fun endRoomRemote() {
        val ref = roomRef ?: return
        viewModelScope.launch {
            try { ref.delete().await() } catch (e: Throwable) {}
        }
    }

    private fun cleanupCall() {
        if (connectedOnce) {
            Store.statsCalls += 1
            Store.statsMinutes += callSeconds / 60
        }
        callTimer?.cancel()
        timeoutJob?.cancel()
        roomListener?.remove(); roomListener = null
        iceListener?.remove(); iceListener = null
        try { dc?.close() } catch (e: Throwable) {}
        dc = null
        dcOpen = false
        engine.endCall()
        engine.stopCapture()
        try { remoteTrack?.removeSink(nullSink) } catch (e: Throwable) {}
        remoteTrack = null
        remoteHasVideo = false
        peerCamOn = true
        sessionKey = null
        keyPair = null
        peerPub = null
        connectedOnce = false
        offerApplied = false
        answerApplied = false
        roomId = null
        roomRef = null
        callSeconds = 0
        quality = 0
        peerTyping = false
        chatOpen = false
        peer = PeerInfo()
        if (phase != Phase.IDLE) expectEnd = false
    }

    private val nullSink = org.webrtc.VideoSink { }

    // ═════════════════════════════════════════════ чат (DataChannel + E2E)

    private fun registerDc(ch: DataChannel) {
        ch.registerObserver(object : DataChannel.Observer {
            override fun onBufferedAmountChange(amount: Long) {}
            override fun onStateChange() {
                dcOpen = ch.state() == DataChannel.State.OPEN
            }

            override fun onMessage(buffer: DataChannel.Buffer?) {
                val data = buffer?.data ?: return
                val bytes = ByteArray(data.remaining())
                data.get(bytes)
                handleDc(String(bytes, Charsets.UTF_8))
            }
        })
    }

    private fun handleDc(raw: String) {
        try {
            val o = JSONObject(raw)
            when (o.optString("t")) {
                "msg" -> {
                    val text = decrypt(o.optString("e")) ?: return
                    messages.add(ChatMsg(text, mine = false))
                    if (!chatOpen) unread++
                }
                "react" -> {
                    val emoji = decrypt(o.optString("e")) ?: return
                    reaction = emoji
                }
                "typing" -> {
                    peerTyping = true
                    typingJob?.cancel()
                    typingJob = viewModelScope.launch {
                        delay(2500)
                        peerTyping = false
                    }
                }
                "cam" -> {
                    peerCamOn = o.optBoolean("v", true)
                }
            }
        } catch (e: Throwable) {
        }
    }

    private fun decrypt(e: String): String? {
        val k = sessionKey ?: return null
        return E2E.decrypt(k, e)
    }

    private fun sendDc(payload: JSONObject): Boolean {
        val ch = dc
        if (ch == null || !dcOpen) return false
        return try {
            ch.send(DataChannel.Buffer(ByteBuffer.wrap(payload.toString().toByteArray(Charsets.UTF_8)), false))
        } catch (e: Throwable) {
            false
        }
    }

    fun sendMessage(text: String) {
        val t = text.trim()
        if (t.isEmpty()) return
        val k = sessionKey
        if (k == null) {
            toast = tr("Secure channel is not ready yet", "Защищённый канал ещё не готов")
            return
        }
        if (sendDc(JSONObject().put("t", "msg").put("e", E2E.encrypt(k, t)))) {
            messages.add(ChatMsg(t, mine = true))
        } else {
            toast = tr("Channel is not ready yet", "Канал ещё не готов")
        }
    }

    fun sendReaction(emoji: String) {
        val k = sessionKey ?: return
        if (sendDc(JSONObject().put("t", "react").put("e", E2E.encrypt(k, emoji)))) {
            reaction = emoji // показываем и у себя
        }
    }

    fun notifyTyping() {
        val now = System.currentTimeMillis()
        if (now - lastTypingSent < 1500) return
        lastTypingSent = now
        sendDc(JSONObject().put("t", "typing"))
    }

    fun markRead() {
        unread = 0
    }

    // ═════════════════════════════════════════════ кнопки звонка

    fun toggleMic() {
        engine.setMic(!engine.micOn)
    }

    fun toggleCam() {
        val on = !engine.camOn
        engine.setCam(on)
        sendDc(JSONObject().put("t", "cam").put("v", on))
    }

    fun switchCamera() = engine.switchCamera()

    fun toggleBlur() {
        Store.blurCam = !Store.blurCam
        engine.setBlur(Store.blurCam)
    }

    fun toggleSpeaker() {
        Store.speakerOn = !Store.speakerOn
        try {
            audio.isSpeakerphoneOn = Store.speakerOn
        } catch (e: Throwable) {
        }
    }

    // ═════════════════════════════════════════════ модерация

    fun blockPeer() {
        val p = peer
        if (p.uid.isBlank()) return
        Store.block(p.uid, p.nick.ifBlank { p.country })
        viewModelScope.launch {
            try {
                db.collection("users").document(uid)
                    .set(mapOf("blocked" to FieldValue.arrayUnion(p.uid)), SetOptions.merge())
                    .await()
            } catch (e: Throwable) {
            }
        }
        toast = tr("User blocked", "Пользователь заблокирован")
        next()
    }

    fun report(reason: String) {
        val p = peer
        if (p.uid.isBlank()) return
        viewModelScope.launch {
            try {
                db.collection("reports").add(
                    mapOf(
                        "reporter" to uid,
                        "reported" to p.uid,
                        "reason" to reason,
                        "room" to (roomId ?: ""),
                        "ts" to FieldValue.serverTimestamp(),
                    ),
                ).await()
            } catch (e: Throwable) {
            }
        }
        toast = tr("Report sent. Thank you!", "Жалоба отправлена. Спасибо!")
        blockPeer()
    }

    fun likePeer() {
        val p = peer
        if (p.uid.isBlank()) return
        if (!Store.liked.contains(p.uid)) Store.liked.add(p.uid)
        toast = tr("Marked as liked ❤", "Отмечен как понравившийся ❤")
    }

    override fun onCleared() {
        super.onCleared()
        incomingListener?.remove()
        engine.destroy()
    }
}
