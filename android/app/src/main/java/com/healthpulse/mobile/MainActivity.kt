package com.healthpulse.mobile

import android.content.Intent
import android.net.ConnectivityManager
import android.net.Network
import android.net.NetworkCapabilities
import android.os.Bundle
import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.browser.customtabs.CustomTabsIntent
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.CloudSync
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Home
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Tune
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

class MainActivity : ComponentActivity() {
    private val viewModel: HealthPulseViewModel by viewModels()
    private lateinit var connectivityManager: ConnectivityManager
    private val networkCallback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) {
            viewModel.sync()
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU
            && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) {
            requestPermissions(arrayOf(Manifest.permission.POST_NOTIFICATIONS), 1)
        }
        connectivityManager = getSystemService(ConnectivityManager::class.java)
        connectivityManager.registerDefaultNetworkCallback(networkCallback)
        handleCallback(intent)
        setContent {
            val browserUrl by viewModel.browserUrl.collectAsState()
            LaunchedEffect(browserUrl) {
                browserUrl?.let {
                    CustomTabsIntent.Builder().build().launchUrl(this@MainActivity, android.net.Uri.parse(it))
                    viewModel.browserOpened()
                }
            }
            HealthPulseApp(viewModel)
        }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        handleCallback(intent)
    }

    override fun onResume() {
        super.onResume()
        if (::connectivityManager.isInitialized && isOnline()) viewModel.sync()
    }

    override fun onDestroy() {
        if (::connectivityManager.isInitialized) connectivityManager.unregisterNetworkCallback(networkCallback)
        super.onDestroy()
    }

    private fun handleCallback(intent: Intent?) {
        val uri = intent?.data ?: return
        if (uri.scheme == "healthpulse" && uri.host == "auth") {
            lifecycleScope.launch { viewModel.receiveAuthCallback(uri.toString()) }
        }
    }

    private fun isOnline(): Boolean {
        val network = connectivityManager.activeNetwork ?: return false
        return connectivityManager.getNetworkCapabilities(network)
            ?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) == true
    }
}

private enum class Destination { HOME, READINGS, TEMPLATES, SETTINGS, ADD, DETAIL }

@Composable
private fun HealthPulseApp(viewModel: HealthPulseViewModel) {
    val snapshot by viewModel.snapshot.collectAsState()
    var destination by remember { mutableStateOf(Destination.HOME) }
    var selectedReading by remember { mutableStateOf<HealthReading?>(null) }

    HealthPulseTheme {
        Surface(modifier = Modifier.fillMaxSize()) {
            if (snapshot.serverUrl == null || !snapshot.isSignedIn) {
                SetupScreen(
                    snapshot = snapshot,
                    onConfigure = viewModel::configureServer,
                    onSignIn = viewModel::beginSignIn,
                    onClearError = viewModel::clearError
                )
            } else {
                Scaffold(
                    containerColor = MaterialTheme.colorScheme.background,
                    topBar = { AppHeader(snapshot, onSync = viewModel::sync) },
                    bottomBar = {
                        if (destination !in setOf(Destination.ADD, Destination.DETAIL)) {
                            BottomNavigation(destination) { destination = it }
                        }
                    },
                    floatingActionButton = {
                        if (destination == Destination.HOME || destination == Destination.READINGS) {
                            FloatingActionButton(onClick = { destination = Destination.ADD }) {
                                Box(
                                    modifier = Modifier.size(56.dp),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Icon(Icons.Default.Add, contentDescription = "Add reading")
                                }
                            }
                        }
                    }
                ) { padding ->
                    Box(Modifier.padding(padding)) {
                        when (destination) {
                            Destination.HOME -> HomeScreen(snapshot) {
                                selectedReading = it
                                destination = Destination.DETAIL
                            }
                            Destination.READINGS -> ReadingsScreen(snapshot) {
                                selectedReading = it
                                destination = Destination.DETAIL
                            }
                            Destination.TEMPLATES -> TemplatesScreen(
                                snapshot,
                                onTrack = viewModel::setTracking,
                                onReminder = viewModel::saveReminder
                            )
                            Destination.SETTINGS -> SettingsScreen(
                                snapshot = snapshot,
                                onSync = viewModel::sync,
                                onConfigure = viewModel::configureServer,
                                onCheckUpdate = viewModel::checkForUpdate,
                                onSignOut = viewModel::signOut
                            )
                            Destination.ADD -> AddReadingScreen(
                                templates = snapshot.trackedTemplates,
                                onBack = { destination = Destination.HOME },
                                onSave = { template, value, note ->
                                    viewModel.addReading(template, value, note, Instant.now())
                                }
                            )
                            Destination.DETAIL -> selectedReading?.let { reading ->
                                ReadingDetailScreen(
                                    reading = reading,
                                    readings = snapshot.readings.filter { it.templateId == reading.templateId },
                                    onBack = { destination = Destination.READINGS },
                                    onDelete = {
                                        viewModel.deleteReading(reading)
                                        destination = Destination.READINGS
                                    }
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SetupScreen(
    snapshot: AppSnapshot,
    onConfigure: (String) -> Unit,
    onSignIn: () -> Unit,
    onClearError: () -> Unit
) {
    var serverUrl by remember(snapshot.serverUrl) { mutableStateOf(snapshot.serverUrl.orEmpty()) }
    val validationError = AppValidation.serverUrl(serverUrl, BuildConfig.ALLOW_HTTP_SERVERS)
    Column(
        modifier = Modifier.fillMaxSize().padding(28.dp),
        verticalArrangement = Arrangement.Center
    ) {
        Text("HealthPulse", style = MaterialTheme.typography.displaySmall, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(8.dp))
        Text("Your data stays on your own HealthPulse server.")
        Spacer(Modifier.height(28.dp))
        OutlinedTextField(
            value = serverUrl,
            onValueChange = {
                serverUrl = it
                onClearError()
            },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            label = { Text("Server address") },
            placeholder = { Text("https://health.example.com") },
            supportingText = {
                Text(validationError ?: "Use your server address. Local HTTP is allowed in debug builds.")
            },
            isError = validationError != null
        )
        Spacer(Modifier.height(16.dp))
        Button(
            onClick = {
                onConfigure(serverUrl)
                onSignIn()
            },
            enabled = validationError == null,
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("Continue with Google")
        }
        snapshot.errorMessage?.let {
            Spacer(Modifier.height(14.dp))
            Text(it, color = MaterialTheme.colorScheme.error)
        }
    }
}

@Composable
private fun AppHeader(snapshot: AppSnapshot, onSync: () -> Unit) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(start = 20.dp, end = 12.dp, top = 14.dp, bottom = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text("HealthPulse", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.SemiBold)
        Spacer(Modifier.weight(1f))
        if (snapshot.queuedChanges > 0) {
            Text(
                snapshot.queuedChanges.toString() + " queued",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.tertiary
            )
        }
        IconButton(onClick = onSync) {
            val icon = when (snapshot.syncStatus) {
                SyncStatus.SYNCING -> Icons.Default.CloudSync
                SyncStatus.UP_TO_DATE -> Icons.Default.CloudDone
                else -> Icons.Default.CloudOff
            }
            Icon(icon, contentDescription = "Sync status: " + snapshot.syncStatus.name.lowercase())
        }
    }
}

@Composable
private fun BottomNavigation(selected: Destination, onNavigate: (Destination) -> Unit) {
    NavigationBar {
        listOf(
            Destination.HOME to Icons.Default.Home,
            Destination.READINGS to Icons.Default.History,
            Destination.TEMPLATES to Icons.Default.Tune,
            Destination.SETTINGS to Icons.Default.Settings
        ).forEach { (destination, icon) ->
            NavigationBarItem(
                selected = selected == destination,
                onClick = { onNavigate(destination) },
                icon = { Icon(icon, contentDescription = destination.name.lowercase()) },
                label = { Text(destination.name.lowercase().replaceFirstChar { it.titlecase() }) }
            )
        }
    }
}

@Composable
private fun HomeScreen(snapshot: AppSnapshot, onReadingSelected: (HealthReading) -> Unit) {
    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Your trends", style = MaterialTheme.typography.headlineMedium)
            Text(
                if (snapshot.syncStatus == SyncStatus.UP_TO_DATE) "Up to date" else "Offline changes are kept on this device",
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        if (snapshot.trackedTemplates.isEmpty()) {
            item { EmptyCard("Track a measurement", "Choose a template to make adding readings quick.") }
        } else {
            items(snapshot.trackedTemplates, key = { it.id }) { template ->
                val latest = snapshot.readings
                    .filter { it.templateId == template.id }
                    .maxByOrNull { it.recordedAtUtc }
                Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
                    Column(Modifier.padding(18.dp)) {
                        Text(template.name, style = MaterialTheme.typography.titleMedium)
                        Text(template.category, style = MaterialTheme.typography.labelMedium)
                        Spacer(Modifier.height(8.dp))
                        Text(
                            latest?.let { formatValue(it) } ?: "No readings yet",
                            style = MaterialTheme.typography.headlineSmall
                        )
                    }
                }
            }
        }
        item {
            Text("Recent readings", style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(top = 8.dp))
        }
        if (snapshot.recentReadings.isEmpty()) {
            item { EmptyCard("Nothing recorded yet", "Tap the plus button to add your first reading.") }
        } else {
            items(snapshot.recentReadings, key = { it.id }) { ReadingRow(it, onReadingSelected) }
        }
    }
}

@Composable
private fun ReadingsScreen(snapshot: AppSnapshot, onReadingSelected: (HealthReading) -> Unit) {
    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        item { Text("All readings", style = MaterialTheme.typography.headlineMedium) }
        if (snapshot.readings.isEmpty()) {
            item { EmptyCard("No readings", "Your saved readings will appear here, even while offline.") }
        } else {
            items(snapshot.readings.sortedByDescending { it.recordedAtUtc }, key = { it.id }) {
                ReadingRow(it, onReadingSelected)
            }
        }
    }
}

@Composable
private fun ReadingRow(reading: HealthReading, onClick: (HealthReading) -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable { onClick(reading) },
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(Modifier.weight(1f)) {
                Text(reading.templateName, fontWeight = FontWeight.SemiBold)
                Text(formatDate(reading.recordedAtUtc), color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Text(formatValue(reading), style = MaterialTheme.typography.titleMedium)
        }
    }
}

@Composable
private fun TemplatesScreen(
    snapshot: AppSnapshot,
    onTrack: (HealthTemplate, Boolean) -> Unit,
    onReminder: (Reminder) -> Unit
) {
    var reminderTemplate by remember { mutableStateOf<HealthTemplate?>(null) }
    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp)
    ) {
        item {
            Text("Measurements", style = MaterialTheme.typography.headlineMedium)
            Text("Enable a measurement to add it from the floating plus button.")
        }
        items(snapshot.templates, key = { it.id }) { template ->
            Card {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(Modifier.weight(1f)) {
                        Text(template.name, fontWeight = FontWeight.SemiBold)
                        Text(template.category + " · " + template.normalizedUnit)
                    }
                    Switch(
                        checked = template.isTracked,
                        onCheckedChange = {
                            onTrack(template, it)
                            if (it) reminderTemplate = template
                        }
                    )
                }
            }
        }
    }
    reminderTemplate?.let { template ->
        ReminderDialog(
            template = template,
            onDismiss = { reminderTemplate = null },
            onSave = {
                onReminder(it)
                reminderTemplate = null
            }
        )
    }
}

@Composable
private fun AddReadingScreen(
    templates: List<HealthTemplate>,
    onBack: () -> Unit,
    onSave: (HealthTemplate, String, String) -> String?
) {
    var selected by remember(templates) { mutableStateOf(templates.firstOrNull()) }
    var value by remember { mutableStateOf("") }
    var note by remember { mutableStateOf("") }
    var showTemplates by remember { mutableStateOf(false) }
    var submissionError by remember { mutableStateOf<String?>(null) }
    val validation = selected?.let { AppValidation.reading(value, note, Instant.now()) }
    Column(Modifier.fillMaxSize().padding(20.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back") }
            Text("Add reading", style = MaterialTheme.typography.headlineMedium)
        }
        if (selected == null) {
            Spacer(Modifier.height(30.dp))
            EmptyCard("No tracked measurements", "Enable a measurement before adding a reading.")
            return@Column
        }
        Spacer(Modifier.height(18.dp))
        OutlinedButton(onClick = { showTemplates = true }, modifier = Modifier.fillMaxWidth()) {
            Text(selected!!.name + " · " + selected!!.normalizedUnit)
        }
        androidx.compose.material3.DropdownMenu(
            expanded = showTemplates,
            onDismissRequest = { showTemplates = false }
        ) {
            templates.forEach { template ->
                androidx.compose.material3.DropdownMenuItem(
                    text = { Text(template.name + " · " + template.normalizedUnit) },
                    onClick = {
                        selected = template
                        showTemplates = false
                    }
                )
            }
        }
        Spacer(Modifier.height(14.dp))
        OutlinedTextField(
            value = value,
            onValueChange = {
                value = it
                submissionError = null
            },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Value (" + selected!!.normalizedUnit + ")") },
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
            singleLine = true,
            isError = validation != null && value.isNotBlank()
        )
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = note,
            onValueChange = {
                note = it
                submissionError = null
            },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Note (optional)") },
            supportingText = { Text(note.length.toString() + " / 140") },
            isError = note.length > 140
        )
        (submissionError ?: validation)?.let {
            Text(it, color = MaterialTheme.colorScheme.error, modifier = Modifier.padding(top = 8.dp))
        }
        Spacer(Modifier.weight(1f))
        Button(
            onClick = {
                submissionError = onSave(selected!!, value, note)
                if (submissionError == null) onBack()
            },
            enabled = validation == null,
            modifier = Modifier.fillMaxWidth()
        ) {
            Text("Save reading")
        }
    }
}

@Composable
private fun ReadingDetailScreen(
    reading: HealthReading,
    readings: List<HealthReading>,
    onBack: () -> Unit,
    onDelete: () -> Unit
) {
    var confirmDelete by remember { mutableStateOf(false) }
    Column(Modifier.fillMaxSize().padding(20.dp)) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back") }
            Text(reading.templateName, style = MaterialTheme.typography.headlineMedium, modifier = Modifier.weight(1f))
            IconButton(onClick = { confirmDelete = true }) {
                Icon(Icons.Default.Delete, "Delete reading", tint = MaterialTheme.colorScheme.error)
            }
        }
        Spacer(Modifier.height(16.dp))
        Text(formatValue(reading), style = MaterialTheme.typography.displayMedium)
        Text(formatDate(reading.recordedAtUtc), color = MaterialTheme.colorScheme.onSurfaceVariant)
        reading.note?.let {
            Spacer(Modifier.height(12.dp))
            Text(it)
        }
        Spacer(Modifier.height(28.dp))
        Text("Trend · last 30 days", style = MaterialTheme.typography.titleMedium)
        TrendChart(readings)
    }
    if (confirmDelete) {
        AlertDialog(
            onDismissRequest = { confirmDelete = false },
            title = { Text("Delete reading?") },
            text = { Text("It will be removed from this device now and synchronised when online.") },
            confirmButton = { TextButton(onClick = onDelete) { Text("Delete") } },
            dismissButton = { TextButton(onClick = { confirmDelete = false }) { Text("Cancel") } }
        )
    }
}

@Composable
private fun TrendChart(readings: List<HealthReading>) {
    val values = readings
        .filter { runCatching { Instant.parse(it.recordedAtUtc).isAfter(Instant.now().minusSeconds(30L * 86400)) }.getOrDefault(false) }
        .sortedBy { it.recordedAtUtc }
    Card(modifier = Modifier.fillMaxWidth().padding(top = 10.dp)) {
        if (values.size < 2) {
            Text("Two readings are needed to show a trend.", Modifier.padding(20.dp))
        } else {
            val primary = MaterialTheme.colorScheme.primary
            Canvas(modifier = Modifier.fillMaxWidth().height(180.dp).padding(16.dp)) {
                val min = values.minOf { it.value }
                val max = values.maxOf { it.value }
                val span = (max - min).takeIf { it > 0.0 } ?: 1.0
                val path = Path()
                values.forEachIndexed { index, item ->
                    val x = size.width * index / (values.size - 1).toFloat()
                    val y = size.height - ((item.value - min) / span * size.height).toFloat()
                    if (index == 0) path.moveTo(x, y) else path.lineTo(x, y)
                }
                drawPath(path, primary, style = Stroke(width = 6f, cap = StrokeCap.Round))
            }
        }
    }
}

@Composable
private fun SettingsScreen(
    snapshot: AppSnapshot,
    onSync: () -> Unit,
    onConfigure: (String) -> Unit,
    onCheckUpdate: (String, (UpdateCheck) -> Unit) -> Unit,
    onSignOut: () -> Unit
) {
    val context = LocalContext.current
    var showChangeServer by remember { mutableStateOf(false) }
    var updateMessage by remember { mutableStateOf<String?>(null) }
    LazyColumn(
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        item { Text("Settings", style = MaterialTheme.typography.headlineMedium) }
        item {
            Text("Server", style = MaterialTheme.typography.labelLarge)
            Text(snapshot.serverUrl.orEmpty(), color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        item { OutlinedButton(onClick = onSync, modifier = Modifier.fillMaxWidth()) {
            Icon(Icons.Default.Refresh, null)
            Spacer(Modifier.width(8.dp))
            Text("Sync now")
        } }
        item { OutlinedButton(onClick = { showChangeServer = true }, modifier = Modifier.fillMaxWidth()) {
            Text("Change server")
        } }
        item { OutlinedButton(
            onClick = {
                onCheckUpdate(BuildConfig.VERSION_NAME) { result ->
                    updateMessage = when (result) {
                        UpdateCheck.UpToDate -> "You have the latest version."
                        UpdateCheck.Unavailable -> "An update could not be checked right now."
                        is UpdateCheck.Available -> {
                            context.startActivity(Intent(Intent.ACTION_VIEW, android.net.Uri.parse(result.apkUrl)))
                            "Version " + result.version + " is ready to download."
                        }
                    }
                }
            },
            modifier = Modifier.fillMaxWidth()
        ) { Text("Check for updates") } }
        item { TextButton(onClick = onSignOut, modifier = Modifier.fillMaxWidth()) { Text("Sign out and clear this device") } }
        updateMessage?.let { message -> item { Text(message, color = MaterialTheme.colorScheme.onSurfaceVariant) } }
    }
    if (showChangeServer) {
        ChangeServerDialog(
            current = snapshot.serverUrl.orEmpty(),
            onDismiss = { showChangeServer = false },
            onConfirm = {
                onConfigure(it)
                showChangeServer = false
            }
        )
    }
}

@Composable
private fun ChangeServerDialog(current: String, onDismiss: () -> Unit, onConfirm: (String) -> Unit) {
    var value by remember { mutableStateOf(current) }
    val error = AppValidation.serverUrl(value, BuildConfig.ALLOW_HTTP_SERVERS)
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Change server?") },
        text = {
            Column {
                Text("This signs you out and clears cached health data from this device.")
                Spacer(Modifier.height(12.dp))
                OutlinedTextField(value, { value = it }, label = { Text("Server address") }, isError = error != null)
                error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            }
        },
        confirmButton = { TextButton(onClick = { onConfirm(value) }, enabled = error == null) { Text("Change server") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } }
    )
}

@Composable
private fun ReminderDialog(template: HealthTemplate, onDismiss: () -> Unit, onSave: (Reminder) -> Unit) {
    var cadence by remember { mutableStateOf(ReminderCadence.DAILY) }
    var interval by remember { mutableStateOf("2") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Remind me to record " + template.name + "?") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                ReminderCadence.entries.forEach {
                    OutlinedButton(onClick = { cadence = it }, modifier = Modifier.fillMaxWidth()) {
                        Text(if (cadence == it) "✓ " + it.name.lowercase().replaceFirstChar { c -> c.titlecase() } else it.name.lowercase().replaceFirstChar { c -> c.titlecase() })
                    }
                }
                if (cadence == ReminderCadence.CUSTOM) {
                    OutlinedTextField(
                        value = interval,
                        onValueChange = { interval = it.filter(Char::isDigit).take(3) },
                        label = { Text("Every how many days?") },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                    )
                }
            }
        },
        confirmButton = {
            TextButton(onClick = {
                onSave(Reminder(template.id, cadence, if (cadence == ReminderCadence.CUSTOM) interval.toIntOrNull()?.coerceIn(1, 365) ?: 1 else if (cadence == ReminderCadence.WEEKLY) 7 else 1))
            }) { Text("Set reminder") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Not now") } }
    )
}

@Composable
private fun EmptyCard(title: String, body: String) {
    Card(colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)) {
        Column(Modifier.padding(20.dp)) {
            Text(title, style = MaterialTheme.typography.titleMedium)
            Spacer(Modifier.height(4.dp))
            Text(body, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

private fun formatValue(reading: HealthReading): String =
    (if (reading.value % 1.0 == 0.0) reading.value.toInt().toString() else "%.2f".format(reading.value)) + " " + reading.unit

private fun formatDate(value: String): String = runCatching {
    DateTimeFormatter.ofPattern("d MMM yyyy, HH:mm")
        .format(Instant.parse(value).atZone(ZoneId.systemDefault()))
}.getOrDefault(value)

@Composable
private fun HealthPulseTheme(content: @Composable () -> Unit) {
    val colors = androidx.compose.material3.darkColorScheme(
        primary = Color(0xFF93D5BF),
        onPrimary = Color(0xFF002119),
        secondary = Color(0xFFAFCFC2),
        tertiary = Color(0xFFD6C285),
        background = Color(0xFF101112),
        surface = Color(0xFF191C1B),
        surfaceVariant = Color(0xFF2B302E),
        onBackground = Color(0xFFE2E4E1)
    )
    MaterialTheme(colorScheme = colors, content = content)
}
