package m.chatrulet.m

import android.content.Context
import org.webrtc.AudioSource
import org.webrtc.AudioTrack
import org.webrtc.Camera2Enumerator
import org.webrtc.CameraVideoCapturer
import org.webrtc.DataChannel
import org.webrtc.DefaultVideoDecoderFactory
import org.webrtc.DefaultVideoEncoderFactory
import org.webrtc.EglBase
import org.webrtc.IceCandidate
import org.webrtc.MediaConstraints
import org.webrtc.MediaStream
import org.webrtc.PeerConnection
import org.webrtc.PeerConnectionFactory
import org.webrtc.RtpReceiver
import org.webrtc.SdpObserver
import org.webrtc.SessionDescription
import org.webrtc.SurfaceTextureHelper
import org.webrtc.VideoFrame
import org.webrtc.VideoFrame.I420Buffer
import org.webrtc.VideoProcessor
import org.webrtc.VideoSink
import org.webrtc.VideoSource
import org.webrtc.VideoTrack

/** SdpObserver с пустыми реализациями, чтобы переопределять только нужное. */
open class SimpleSdp : SdpObserver {
    override fun onCreateSuccess(sdp: SessionDescription?) {}
    override fun onSetSuccess() {}
    override fun onCreateFailure(error: String?) {}
    override fun onSetFailure(error: String?) {}
}

/**
 * Программный «матовый» блюр своей камеры: двухпроходный box-blur по яркостной
 * плоскости Y (I420). Вешается на VideoSource и работает в реальном времени.
 */
class BlurVideoProcessor : VideoProcessor {
    @Volatile var enabled = false
    private var sink: VideoSink? = null

    override fun setSink(s: VideoSink?) {
        sink = s
    }

    override fun onCapturerStarted(success: Boolean) {}
    override fun onCapturerStopped() {}

    override fun onFrameCaptured(frame: VideoFrame) {
        val s = sink
        if (s == null) {
            frame.release()
            return
        }
        if (!enabled) {
            s.onFrame(frame)
            frame.release()
            return
        }
        val i420 = frame.buffer.toI420()
        if (i420 == null) {
            s.onFrame(frame)
            frame.release()
            return
        }
        try {
            blurLuma(i420)
            val out = VideoFrame(i420, frame.rotation, frame.timestampNs)
            s.onFrame(out)
            out.release()
        } catch (t: Throwable) {
            i420.release()
        }
        frame.release()
    }

    private fun blurLuma(buf: I420Buffer) {
        val w = buf.strideY
        val h = buf.height
        if (w <= 0 || h <= 0) return
        val data = buf.dataY
        val arr = ByteArray(w * h)
        for (y in 0 until h) {
            data.position(y * w)
            data.get(arr, y * w, w)
        }
        val r = (h / 28).coerceIn(6, 18)
        val tmp = ByteArray(w * h)
        repeat(2) {
            boxH(arr, tmp, w, h, r)
            boxV(tmp, arr, w, h, r)
        }
        for (y in 0 until h) {
            data.position(y * w)
            data.put(arr, y * w, w)
        }
    }

    private fun boxH(src: ByteArray, dst: ByteArray, w: Int, h: Int, r: Int) {
        val div = 2 * r + 1
        for (y in 0 until h) {
            val off = y * w
            var sum = 0
            var i = -r
            while (i <= r) {
                sum += src[off + i.coerceIn(0, w - 1)].toInt() and 0xFF
                i++
            }
            for (x in 0 until w) {
                dst[off + x] = (sum / div).toByte()
                sum += src[off + (x + r + 1).coerceAtMost(w - 1)].toInt() and 0xFF
                sum -= src[off + (x - r).coerceIn(0, w - 1)].toInt() and 0xFF
            }
        }
    }

    private fun boxV(src: ByteArray, dst: ByteArray, w: Int, h: Int, r: Int) {
        val div = 2 * r + 1
        for (x in 0 until w) {
            var sum = 0
            var i = -r
            while (i <= r) {
                sum += src[i.coerceIn(0, h - 1) * w + x].toInt() and 0xFF
                i++
            }
            for (y in 0 until h) {
                dst[y * w + x] = (sum / div).toByte()
                sum += src[(y + r + 1).coerceAtMost(h - 1) * w + x].toInt() and 0xFF
                sum -= src[(y - r).coerceIn(0, h - 1) * w + x].toInt() and 0xFF
            }
        }
    }
}

/**
 * Обёртка над WebRTC: фабрика, камера/микрофон, PeerConnection,
 * статистика (RTT → индикатор качества).
 */
class RtcEngine(private val app: Context) {

    interface Events {
        fun onIce(candidate: IceCandidate)
        fun onConnected()
        fun onDisconnected(reason: String)
        fun onDataChannel(dc: DataChannel)
        fun onRemoteVideo(track: VideoTrack)
    }

    var events: Events? = null

    val egl: EglBase = EglBase.create()
    val blur = BlurVideoProcessor()

    private var factory: PeerConnectionFactory? = null
    var pc: PeerConnection? = null
        private set
    private var capturer: CameraVideoCapturer? = null
    private var videoSource: VideoSource? = null
    private var audioSource: AudioSource? = null
    var localVideo: VideoTrack? = null
        private set
    private var localAudio: AudioTrack? = null
    private var helper: SurfaceTextureHelper? = null

    var micOn = true
        private set
    var camOn = true
        private set
    var front = true
        private set
    var started = false
        private set

    @Synchronized
    fun ensureStarted() {
        if (started) return
        try {
            PeerConnectionFactory.initialize(
                PeerConnectionFactory.InitializationOptions.builder(app)
                    .setEnableInternalTracer(false)
                    .createInitializationOptions(),
            )
            val f = PeerConnectionFactory.builder()
                .setVideoEncoderFactory(DefaultVideoEncoderFactory(egl.eglBaseContext, true, true))
                .setVideoDecoderFactory(DefaultVideoDecoderFactory(egl.eglBaseContext))
                .createPeerConnectionFactory()
            factory = f
            val vs = f.createVideoSource(false)
            vs.setVideoProcessor(blur)
            videoSource = vs
            audioSource = f.createAudioSource(MediaConstraints())
            localVideo = f.createVideoTrack("v0", vs)
            localAudio = f.createAudioTrack("a0", audioSource)
            val h = SurfaceTextureHelper.create("capture", egl.eglBaseContext)
            helper = h
            val cap = createCapturer()
            capturer = cap
            cap?.initialize(h, app, vs.capturerObserver)
            try {
                cap?.startCapture(640, 480, 24)
            } catch (t: Throwable) {
                // эмулятор/устройство без камеры
            }
            started = true
        } catch (t: Throwable) {
            // нет камеры — звонок всё равно пойдёт со звуком
            started = true
        }
    }

    private fun createCapturer(): CameraVideoCapturer? = try {
        val e = Camera2Enumerator(app)
        val names = e.deviceNames
        val name = names.firstOrNull { e.isFrontFacing(it) } ?: names.firstOrNull() ?: return null
        e.createCapturer(name, null)
    } catch (t: Throwable) {
        null
    }

    fun resumeCapture() {
        if (!started) return
        try {
            capturer?.startCapture(640, 480, 24)
        } catch (t: Throwable) {
        }
    }

    fun stopCapture() {
        try {
            capturer?.stopCapture()
        } catch (t: Throwable) {
        }
    }

    fun switchCamera() {
        try {
            capturer?.switchCamera(null)
            front = !front
        } catch (t: Throwable) {
        }
    }

    fun setMic(on: Boolean) {
        micOn = on
        localAudio?.setEnabled(on)
    }

    fun setCam(on: Boolean) {
        camOn = on
        localVideo?.setEnabled(on)
    }

    fun setBlur(on: Boolean) {
        blur.enabled = on
    }

    fun createPeer(): PeerConnection? {
        val f = factory ?: return null
        val ice = listOf(
            PeerConnection.IceServer.builder("stun:stun.l.google.com:19302").createIceServer(),
            PeerConnection.IceServer.builder("stun:stun1.l.google.com:19302").createIceServer(),
            PeerConnection.IceServer.builder("stun:stun2.l.google.com:19302").createIceServer(),
            // Публичный бесплатный TURN (Open Relay) — нужен для мобильных NAT,
            // своего сервера не требуется.
            PeerConnection.IceServer.builder(
                listOf(
                    "turn:openrelay.metered.ca:80",
                    "turn:openrelay.metered.ca:443",
                    "turn:openrelay.metered.ca:443?transport=tcp",
                ),
            ).setUsername("openrelayproject").setPassword("openrelayproject").createIceServer(),
        )
        val cfg = PeerConnection.RTCConfiguration(ice).apply {
            sdpSemantics = PeerConnection.SdpSemantics.UNIFIED_PLAN
            continualGatheringPolicy = PeerConnection.ContinualGatheringPolicy.GATHER_CONTINUALLY
        }
        val peer = try {
            f.createPeerConnection(cfg, object : PeerConnection.Observer {
                override fun onSignalingChange(state: PeerConnection.SignalingState?) {}
                override fun onIceConnectionChange(state: PeerConnection.IceConnectionState?) {
                    when (state) {
                        PeerConnection.IceConnectionState.CONNECTED,
                        PeerConnection.IceConnectionState.COMPLETED,
                        -> events?.onConnected()

                        PeerConnection.IceConnectionState.DISCONNECTED,
                        PeerConnection.IceConnectionState.FAILED,
                        PeerConnection.IceConnectionState.CLOSED,
                        -> events?.onDisconnected(state?.name ?: "?")

                        else -> {}
                    }
                }

                override fun onIceConnectionReceivingChange(receiving: Boolean) {}
                override fun onIceGatheringChange(state: PeerConnection.IceGatheringState?) {}
                override fun onIceCandidate(candidate: IceCandidate?) {
                    candidate?.let { events?.onIce(it) }
                }

                override fun onIceCandidatesRemoved(candidates: Array<out IceCandidate>?) {}
                override fun onAddStream(stream: MediaStream?) {
                    stream?.videoTracks?.firstOrNull()?.let { events?.onRemoteVideo(it) }
                }

                override fun onRemoveStream(stream: MediaStream?) {}
                override fun onDataChannel(dc: DataChannel?) {
                    dc?.let { events?.onDataChannel(it) }
                }

                override fun onRenegotiationNeeded() {}
                override fun onAddTrack(receiver: RtpReceiver?, streams: Array<out MediaStream>?) {
                    (receiver?.track() as? VideoTrack)?.let { events?.onRemoteVideo(it) }
                }
            })
        } catch (t: Throwable) {
            null
        } ?: return null
        localVideo?.let { peer.addTrack(it, listOf("stream")) }
        localAudio?.let { peer.addTrack(it, listOf("stream")) }
        pc = peer
        return peer
    }

    /** RTT в миллисекундах по выбранной ICE-паре, или -1. */
    fun pollRtt(cb: (Int) -> Unit) {
        val peer = pc ?: return
        try {
            peer.getStats { report ->
                var best = -1.0
                try {
                    for (s in report.statsMap.values) {
                        if (s.type == "candidate-pair") {
                            val nominated = s.members["nominated"]
                            if (nominated == true) {
                                val rtt = s.members["currentRoundTripTime"] as? Double ?: continue
                                if (best < 0 || rtt < best) best = rtt
                            }
                        }
                    }
                } catch (t: Throwable) {
                }
                cb(if (best >= 0) (best * 1000).toInt() else -1)
            }
        } catch (t: Throwable) {
        }
    }

    fun endCall() {
        try {
            pc?.close()
        } catch (t: Throwable) {
        }
        pc = null
    }

    fun destroy() {
        endCall()
        try { capturer?.stopCapture() } catch (t: Throwable) {}
        try { capturer?.dispose() } catch (t: Throwable) {}
        try { helper?.dispose() } catch (t: Throwable) {}
        try { videoSource?.dispose() } catch (t: Throwable) {}
        try { audioSource?.dispose() } catch (t: Throwable) {}
        try { factory?.dispose() } catch (t: Throwable) {}
        try { egl.release() } catch (t: Throwable) {}
        started = false
    }
}
