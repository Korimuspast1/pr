package com.zalchik.nimbus.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import com.zalchik.nimbus.data.Server
import com.zalchik.nimbus.engine.Balancers
import com.zalchik.nimbus.engine.Ping
import com.zalchik.nimbus.i18n.I18n
import com.zalchik.nimbus.ui.NiIcons
import com.zalchik.nimbus.ui.components.Caption
import com.zalchik.nimbus.ui.components.Chip
import com.zalchik.nimbus.ui.components.EmptyState
import com.zalchik.nimbus.ui.components.NimbusCard
import com.zalchik.nimbus.ui.state.AppState
import com.zalchik.nimbus.ui.theme.MonoStyle
import com.zalchik.nimbus.ui.theme.NimbusTheme

/**
 * The nodes of one group the core reports: Nimbus' own balancer, or a provider's selector inside
 * a full config.
 *
 * Which of them can be switched by hand is the core's rule, not ours: sing-box answers a node
 * choice only for a `selector` group (`SelectOutbound` casts to `group.Selector`), while a
 * `urltest` group — what a Nimbus balancer is — keeps choosing the fastest node itself. So a
 * urltest group is shown with its measured delays and a "check now" button, and a selector group
 * with rows you can tap.
 */
@Composable
fun NodePickerScreen(app: AppState, groupTag: String, onBack: () -> Unit) {
    val colors = NimbusTheme.colors
    val service by app.service.collectAsState()
    val group = service.groups.firstOrNull { it.tag == groupTag }

    // A balancer's members give the node tags their real names; a provider's group has none.
    val members: List<Server> = app.selected()
        ?.let { (it as? com.zalchik.nimbus.engine.Selected.OfBalancer)?.members }
        ?: emptyList()

    ScreenScaffold(
        title = I18n.tr("Узлы"),
        onBack = onBack,
        actions = {
            if (group != null) {
                Chip(I18n.tr("Проверить"), NiIcons.Timer) { app.testGroup(group.tag) }
            }
        },
    ) {
        Column(
            Modifier
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            if (group == null) {
                // The tunnel went down, or the config it runs has no such group any more.
                EmptyState(
                    icon = NiIcons.CloudOff,
                    text = I18n.tr("Узлы видны, пока подключение активно."),
                )
                return@Column
            }

            NimbusCard {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(
                        I18n.tr("Группа «{tag}»", values = mapOf("tag" to group.tag)),
                        style = MaterialTheme.typography.titleSmall,
                        color = colors.text,
                    )
                    Text(
                        if (group.selectable) {
                            I18n.tr("Узел можно выбрать самим.")
                        } else {
                            I18n.tr("Этот узел выбирает sing-box: он идёт через самый быстрый.")
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.muted,
                    )
                }
            }

            Caption(I18n.plural(group.items.size, "{n} узел", "{n} узла", "{n} узлов")
                .replace("{n}", group.items.size.toString()))

            NimbusCard {
                Column {
                    for (item in group.items) {
                        val current = item.tag == group.selected ||
                            (!group.selectable && item.tag == service.currentNode)
                        val name = Balancers.memberOf(item.tag, members)?.name ?: Balancers.plain(item.tag)
                        Row(
                            Modifier
                                .fillMaxWidth()
                                .then(
                                    if (group.selectable) {
                                        Modifier.clickable {
                                            app.selectNode(group.tag, item.tag)
                                            app.say(I18n.tr("Узел переключён"))
                                        }
                                    } else {
                                        Modifier
                                    }
                                )
                                .padding(horizontal = 16.dp, vertical = 13.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Box(
                                Modifier
                                    .size(7.dp)
                                    .clip(RoundedCornerShape(4.dp))
                                    .background(
                                        when {
                                            !current -> colors.border2
                                            item.delay > 0 -> colors.ok
                                            else -> colors.text2
                                        }
                                    )
                            )
                            Spacer(Modifier.width(12.dp))
                            Column(Modifier.weight(1f)) {
                                Text(
                                    name,
                                    style = MaterialTheme.typography.bodyLarge,
                                    color = if (current) colors.text else colors.text2,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis,
                                )
                                if (item.type.isNotEmpty() && item.type != group.type) {
                                    Text(
                                        item.type,
                                        style = MaterialTheme.typography.labelSmall,
                                        color = colors.faint,
                                    )
                                }
                            }
                            if (current) {
                                Icon(NiIcons.Check, null, tint = colors.ok, modifier = Modifier.size(16.dp))
                                Spacer(Modifier.width(10.dp))
                            }
                            Text(
                                // A node the core hasn't measured yet reads as "…", a dead one as "—".
                                Ping.format(item.delay.takeIf { it > 0 } ?: if (item.testedAt > 0) -1 else null),
                                style = MonoStyle,
                                color = colors.muted,
                            )
                        }
                    }
                }
            }

            if (service.delays.isNotEmpty()) {
                val best = Balancers.bestDelay(group.items.map { it.tag to it.delay })
                if (best != null) {
                    Text(
                        I18n.tr("Лучшая задержка: {ms}", values = mapOf("ms" to Ping.format(best))),
                        style = MaterialTheme.typography.bodySmall,
                        color = colors.muted,
                    )
                }
            }
        }
    }
}
