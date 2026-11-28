package com.rootdb.pro.ui.screens

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Tune
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.rootdb.pro.data.AppScanner
import com.rootdb.pro.data.DirEntry
import com.rootdb.pro.data.FileStat
import com.rootdb.pro.data.RootShell
import com.rootdb.pro.data.formatDateTime
import com.rootdb.pro.data.formatSize
import com.rootdb.pro.ui.components.EmptyBox
import com.rootdb.pro.ui.components.LoadingBox
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.w3c.dom.Document
import org.w3c.dom.Element
import java.io.File
import javax.xml.parsers.DocumentBuilderFactory
import javax.xml.transform.OutputKeys
import javax.xml.transform.TransformerFactory
import javax.xml.transform.dom.DOMSource
import javax.xml.transform.stream.StreamResult

private data class PrefEntry(val key: String, val type: String, val value: String)

/** Редактор SharedPreferences через root */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PrefsScreen(pkg: String, onBack: () -> Unit) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbar = remember { SnackbarHostState() }

    var files by remember { mutableStateOf<List<DirEntry>>(emptyList()) }
    var loading by remember { mutableStateOf(true) }
    var selected by remember { mutableStateOf<DirEntry?>(null) }
    var entries by remember { mutableStateOf<List<PrefEntry>>(emptyList()) }
    var doc by remember { mutableStateOf<Document?>(null) }
    var origStat by remember { mutableStateOf<FileStat?>(null) }
    var dirty by remember { mutableStateOf(false) }
    var editEntry by remember { mutableStateOf<PrefEntry?>(null) }
    var editText by remember { mutableStateOf("") }
    var editBool by remember { mutableStateOf(false) }

    suspend fun loadFiles() {
        loading = true
        files = withContext(Dispatchers.IO) { AppScanner.listPrefs(pkg) }
        loading = false
    }

    LaunchedEffect(pkg) { loadFiles() }

    fun parseEntries(d: Document): List<PrefEntry> {
        val out = mutableListOf<PrefEntry>()
        val map = d.documentElement ?: return out
        for (i in 0 until map.childNodes.length) {
            val node = map.childNodes.item(i)
            if (node !is Element) continue
            val key = node.getAttribute("name")
            if (key.isEmpty()) continue
            val type = node.tagName
            val value = when (type) {
                "string" -> node.textContent ?: ""
                "set" -> {
                    val items = mutableListOf<String>()
                    for (j in 0 until node.childNodes.length) {
                        val item = node.childNodes.item(j)
                        if (item is Element && item.tagName == "item") items += item.textContent ?: ""
                    }
                    items.joinToString(", ")
                }
                else -> node.getAttribute("value")
            }
            out += PrefEntry(key, type, value)
        }
        return out.sortedBy { it.key }
    }

    suspend fun openFile(entry: DirEntry) {
        loading = true
        try {
            val dir = File(context.cacheDir, "prefs/$pkg").apply { mkdirs() }
            val copy = File(dir, entry.name)
            if (!withContext(Dispatchers.IO) { RootShell.copyToAppDir(entry.path, copy) }) {
                throw Exception("Не удалось скопировать через root")
            }
            origStat = withContext(Dispatchers.IO) { RootShell.stat(entry.path) }
            val parsed = withContext(Dispatchers.IO) {
                val factory = DocumentBuilderFactory.newInstance().apply { isNamespaceAware = false }
                factory.newDocumentBuilder().parse(copy)
            }
            doc = parsed
            entries = parseEntries(parsed)
            selected = entry
            dirty = false
        } catch (e: Exception) {
            snackbar.showSnackbar("Ошибка: ${e.message}")
        }
        loading = false
    }

    fun applyEdit(entry: PrefEntry, newValue: String, newBool: Boolean) {
        val d = doc ?: return
        val map = d.documentElement ?: return
        for (i in 0 until map.childNodes.length) {
            val node = map.childNodes.item(i)
            if (node !is Element) continue
            if (node.getAttribute("name") != entry.key) continue
            when (entry.type) {
                "string" -> node.textContent = newValue
                "boolean" -> node.setAttribute("value", newBool.toString())
                "int", "long", "float" -> node.setAttribute("value", newValue)
                "set" -> {
                    // Пересоздаём set из строк через запятую
                    while (node.firstChild != null) node.removeChild(node.firstChild)
                    newValue.split(",").map { it.trim() }.filter { it.isNotEmpty() }.forEach { item ->
                        val el = d.createElement("item")
                        el.textContent = item
                        node.appendChild(el)
                    }
                }
            }
        }
        entries = parseEntries(d)
        dirty = true
    }

    fun serialize(d: Document): String {
        val transformer = TransformerFactory.newInstance().newTransformer().apply {
            setOutputProperty(OutputKeys.INDENT, "yes")
            setOutputProperty(OutputKeys.ENCODING, "UTF-8")
        }
        val writer = java.io.StringWriter()
        transformer.transform(DOMSource(d), StreamResult(writer))
        return writer.toString()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(if (selected == null) "SharedPreferences" else selected!!.name) },
                navigationIcon = {
                    IconButton(onClick = { if (selected != null) selected = null else onBack() }) {
                        Icon(Icons.Filled.ArrowBack, contentDescription = "Назад")
                    }
                },
                actions = {
                    if (selected != null && dirty) {
                        IconButton(onClick = {
                            scope.launch {
                                try {
                                    val d = doc ?: return@launch
                                    val xml = withContext(Dispatchers.IO) { serialize(d) }
                                    val st = origStat
                                    val ok = withContext(Dispatchers.IO) {
                                        if (st != null) RootShell.writeTextFile(selected!!.path, xml, st.mode, st.uid, st.gid)
                                        else RootShell.writeTextFile(selected!!.path, xml, 660, 0, 0)
                                    }
                                    if (ok) {
                                        dirty = false
                                        snackbar.showSnackbar("Сохранено. Перезапустите приложение, чтобы изменения применились.")
                                    } else {
                                        snackbar.showSnackbar("Не удалось записать через root")
                                    }
                                } catch (e: Exception) {
                                    snackbar.showSnackbar("Ошибка: ${e.message}")
                                }
                            }
                        }) { Icon(Icons.Filled.Save, contentDescription = "Сохранить") }
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        when {
            loading -> LoadingBox("Читаю через root…")
            selected == null -> {
                if (files.isEmpty()) EmptyBox("SharedPreferences не найдены")
                else LazyColumn(Modifier.fillMaxSize().padding(padding)) {
                    items(files, key = { it.path }) { f ->
                        ListItem(
                            headlineContent = { Text(f.name, fontWeight = FontWeight.Bold) },
                            supportingContent = { Text("${formatSize(f.size)} • ${formatDateTime(f.modifiedSec)}") },
                            leadingContent = { Icon(Icons.Filled.Tune, null, tint = MaterialTheme.colorScheme.primary) },
                            modifier = Modifier.clickable { scope.launch { openFile(f) } }
                        )
                    }
                }
            }
            else -> LazyColumn(Modifier.fillMaxSize().padding(padding)) {
                items(entries, key = { it.key }) { e ->
                    Card(
                        shape = MaterialTheme.shapes.medium,
                        modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp)
                            .clickable {
                                editEntry = e
                                editText = e.value
                                editBool = e.value == "true"
                            }
                    ) {
                        ListItem(
                            headlineContent = { Text(e.key, fontWeight = FontWeight.Bold) },
                            supportingContent = { Text("${e.type}: ${e.value.take(200)}") }
                        )
                    }
                }
            }
        }
    }

    editEntry?.let { e ->
        AlertDialog(
            onDismissRequest = { editEntry = null },
            title = { Text(e.key) },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Тип: ${e.type}", style = MaterialTheme.typography.bodySmall)
                    if (e.type == "boolean") {
                        Row {
                            Text("Значение")
                            Switch(checked = editBool, onCheckedChange = { editBool = it })
                        }
                    } else {
                        OutlinedTextField(
                            value = editText,
                            onValueChange = { editText = it },
                            label = { Text("Значение") },
                            modifier = Modifier.fillMaxWidth()
                        )
                    }
                }
            },
            confirmButton = {
                Button(onClick = {
                    applyEdit(e, editText, editBool)
                    editEntry = null
                }) { Text("Применить") }
            },
            dismissButton = { TextButton(onClick = { editEntry = null }) { Text("Отмена") } }
        )
    }
}
