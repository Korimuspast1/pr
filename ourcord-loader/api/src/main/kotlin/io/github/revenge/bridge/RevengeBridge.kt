package app.ourcord.bridge

/**
 * - JS -> native: `{ ourcord: { method, args: [...] } }` passed to either `RNSVGRenderableManager.getBBox` (sync, fast)
 *   or `FileReaderModule.readAsDataURL` (alternative path).
 *
 * - Native -> JS: JS registers a callable module named `OurCordBridge`; native code invokes
 *   `ReactInstance.callFunctionOnModule("OurCordBridge", method, NativeArray)`.
 *   JS replies via `ourcord.__callableReturn`.
 */
interface OurCordBridge {
    /**
     * Register a native method callable from JS.
     *
     * If [name] is already registered, the new handler replaces the old one and a warning is logged.
     * 
     * Arguments and return values are converted by React Native:
     * https://github.com/facebook/react-native/blob/main/packages/react-native/ReactAndroid/src/main/java/com/facebook/react/bridge/Arguments.kt
     *
     * Additionally, `Unit` is converted to `null`.
     */
    fun registerMethod(name: String, handler: (args: List<Any?>) -> Any?)

    /**
     * Register a **suspending** native method callable from JS.
     *
     * Only callable through the async (promise-based) bridge path.
     * The handler is dispatched off the React native-modules thread, as to not block the bridge.
     *
     * Semantics and argument/return conversion match [registerMethod].
     */
    fun registerAsyncMethod(name: String, handler: suspend (args: List<Any?>) -> Any?)

    /**
     * Invoke a JS method on the `OurCordBridge` callable module and await JS's `ourcord.__callableReturn` reply.
     *
     * Throws if JS responds with `{ error: ... }` or if JavaScript isn't ready. May suspend forever if JS never replies.
     */
    suspend fun callJSMethod(name: String, args: List<Any?> = emptyList()): Any?
}
