package com.zalchik.nimbus.bg

import android.net.IpPrefix
import io.nekohasekai.libbox.LogEntry
import io.nekohasekai.libbox.LogIterator
import io.nekohasekai.libbox.NeighborEntry
import io.nekohasekai.libbox.NeighborEntryIterator
import io.nekohasekai.libbox.NetworkInterface
import io.nekohasekai.libbox.NetworkInterfaceIterator
import io.nekohasekai.libbox.RoutePrefix
import io.nekohasekai.libbox.StringBox
import io.nekohasekai.libbox.StringIterator
import java.net.InetAddress
import kotlin.coroutines.Continuation

/**
 * The small adapters libbox's generated Java interfaces need: gobind speaks in iterators and
 * proxies, Kotlin in lists. Same helpers the reference Android client carries.
 */

val StringBox?.unwrap: String get() = this?.value ?: ""

fun StringIterator.toList(): List<String> = mutableListOf<String>().also { out ->
    while (hasNext()) out.add(next())
}

fun LogIterator.toList(): List<LogEntry> = mutableListOf<LogEntry>().also { out ->
    while (hasNext()) out.add(next())
}

fun RoutePrefix.toIpPrefix(): IpPrefix = IpPrefix(InetAddress.getByName(address()), prefix())

class StringArray(private val iterator: Iterator<String>) : StringIterator {
    private val items = iterator.asSequence().toList()
    private var index = 0

    override fun hasNext(): Boolean = index < items.size
    override fun next(): String = items[index++]
    override fun len(): Int = items.size
}

fun Iterable<String>.toStringIterator(): StringIterator = StringArray(iterator())

class InterfaceArray(private val iterator: Iterator<NetworkInterface>) : NetworkInterfaceIterator {
    private val items = iterator.asSequence().toList()
    private var index = 0

    override fun hasNext(): Boolean = index < items.size
    override fun next(): NetworkInterface = items[index++]
}

class NeighborEntryArray(private val iterator: Iterator<NeighborEntry>) : NeighborEntryIterator {
    private val items = iterator.asSequence().toList()
    private var index = 0

    override fun hasNext(): Boolean = index < items.size
    override fun next(): NeighborEntry = items[index++]
}

/** libbox calls back from Go threads; a continuation may already have been resumed by a cancel. */
fun <T> Continuation<T>.tryResume(value: T) {
    try {
        resumeWith(Result.success(value))
    } catch (ignored: IllegalStateException) {
    }
}

fun <T> Continuation<T>.tryResumeWithException(exception: Throwable) {
    try {
        resumeWith(Result.failure(exception))
    } catch (ignored: IllegalStateException) {
    }
}
