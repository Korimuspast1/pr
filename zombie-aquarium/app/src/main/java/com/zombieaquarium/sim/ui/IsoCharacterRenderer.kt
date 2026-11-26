package com.zombieaquarium.sim.ui

import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.RectF
import com.zombieaquarium.sim.ai.CampRole
import com.zombieaquarium.sim.ai.FsmState
import com.zombieaquarium.sim.ai.SurvivorAgent
import com.zombieaquarium.sim.ai.SurvivorTrait
import com.zombieaquarium.sim.ai.ZombieAgent
import com.zombieaquarium.sim.engine.WeaponType
import com.zombieaquarium.sim.engine.WorldSimulation
import com.zombieaquarium.sim.engine.ZombieType
import kotlin.math.cos
import kotlin.math.sin

class IsoCharacterRenderer {
    private val paint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val strokePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.STROKE
        strokeCap = Paint.Cap.ROUND
    }
    private val ovalRect = RectF()

    fun drawSurvivor(
        canvas: Canvas,
        s: SurvivorAgent,
        sim: WorldSimulation,
        sx: Float,
        sy: Float,
        scale: Float,
        renderAlpha: Float,
        selected: Boolean,
        textPaint: Paint,
        bubblePaint: Paint
    ) {
        // World facing converted to isometric screen vector
        val isoFx = s.facingX - s.facingY
        val isoFy = (s.facingX + s.facingY) * 0.5f
        val fl = kotlin.math.sqrt(isoFx * isoFx + isoFy * isoFy).coerceAtLeast(0.01f)
        val dirX = isoFx / fl
        val dirY = isoFy / fl

        // Ground shadow
        paint.color = 0x66000000
        ovalRect.set(sx - 9f * scale, sy - 4f * scale, sx + 9f * scale, sy + 5f * scale)
        canvas.drawOval(ovalRect, paint)

        // Selection or Camp/Infection ring on floor
        if (selected) {
            strokePaint.color = Color.WHITE
            strokePaint.strokeWidth = 2.2f * scale.coerceAtLeast(0.6f)
            ovalRect.set(sx - 13f * scale, sy - 6f * scale, sx + 13f * scale, sy + 7f * scale)
            canvas.drawOval(ovalRect, strokePaint)
        } else if (s.infected) {
            strokePaint.color = 0xFFA3E635.toInt()
            strokePaint.strokeWidth = 1.8f * scale.coerceAtLeast(0.5f)
            ovalRect.set(sx - 11f * scale, sy - 5f * scale, sx + 11f * scale, sy + 6f * scale)
            canvas.drawOval(ovalRect, strokePaint)
        } else {
            val campColor = sim.campsById[s.campId]?.color
            if (campColor != null) {
                strokePaint.color = campColor
                strokePaint.strokeWidth = 1.6f * scale.coerceAtLeast(0.5f)
                ovalRect.set(sx - 11f * scale, sy - 5f * scale, sx + 11f * scale, sy + 6f * scale)
                canvas.drawOval(ovalRect, strokePaint)
            }
        }

        // Sleeping pose
        if (s.fsmState == FsmState.SLEEPING) {
            paint.color = 0xFF334155.toInt()
            ovalRect.set(sx - 11f * scale, sy - 5f * scale, sx + 11f * scale, sy + 3f * scale)
            canvas.drawRoundRect(ovalRect, 3f * scale, 3f * scale, paint)
            paint.color = s.avatarColor
            ovalRect.set(sx - 6f * scale, sy - 5f * scale, sx + 8f * scale, sy + 2f * scale)
            canvas.drawRoundRect(ovalRect, 3f * scale, 3f * scale, paint)
            paint.color = 0xFFFDE68A.toInt()
            canvas.drawCircle(sx - 8f * scale, sy - 2f * scale, 3.6f * scale, paint)
            if (scale >= 0.7f) {
                textPaint.textSize = (11f * scale).coerceIn(14f, 24f)
                textPaint.color = 0xFF93C5FD.toInt()
                canvas.drawText("Zzz", sx + 4f * scale, sy - 9f * scale, textPaint)
            }
            return
        }

        // Walk / sneak / idle bobbing (60 FPS smooth phase)
        val smoothPhase = if (s.isMovingNow) s.animPhase + renderAlpha * 0.55f else s.animPhase
        val stride = if (s.isMovingNow) sin(smoothPhase) * 4.5f * scale else 0f
        val bob = if (s.isMovingNow) kotlin.math.abs(cos(smoothPhase)) * 1.8f * scale else 0f
        val crouch = if (s.fsmState == FsmState.SNEAKING || s.fsmState == FsmState.LOOTING) 2.5f * scale else 0f

        // Legs
        strokePaint.color = 0xFF1E293B.toInt()
        strokePaint.strokeWidth = 3.2f * scale
        canvas.drawLine(sx - 2.5f * scale, sy - 5f * scale + crouch, sx - 2.5f * scale - stride * 0.6f, sy + stride * 0.4f, strokePaint)
        canvas.drawLine(sx + 2.5f * scale, sy - 5f * scale + crouch, sx + 2.5f * scale + stride * 0.6f, sy - stride * 0.4f, strokePaint)

        // Backpack (drawn behind torso when facing forward-ish)
        paint.color = 0xFF78350F.toInt()
        ovalRect.set(sx - dirX * 4f * scale - 4.5f * scale, sy - 16f * scale + crouch - bob, sx - dirX * 4f * scale + 4.5f * scale, sy - 6f * scale + crouch - bob)
        canvas.drawRoundRect(ovalRect, 2.5f * scale, 2.5f * scale, paint)

        // Torso / Jacket
        paint.color = if (s.hurtAnimTicks > 0) 0xFFEF4444.toInt() else s.avatarColor
        ovalRect.set(sx - 5.5f * scale, sy - 17f * scale + crouch - bob, sx + 5.5f * scale, sy - 5f * scale + crouch - bob)
        canvas.drawRoundRect(ovalRect, 3.5f * scale, 3.5f * scale, paint)

        // Medic red cross on chest
        if (s.hasTrait(SurvivorTrait.MEDIC) && scale >= 0.7f) {
            paint.color = Color.WHITE
            canvas.drawRect(sx - 2.5f * scale, sy - 12f * scale + crouch - bob, sx + 2.5f * scale, sy - 10f * scale + crouch - bob, paint)
            paint.color = 0xFFEF4444.toInt()
            canvas.drawRect(sx - 0.8f * scale, sy - 13f * scale + crouch - bob, sx + 0.8f * scale, sy - 9f * scale + crouch - bob, paint)
        }

        // Head
        val headY = sy - 20.5f * scale + crouch - bob
        paint.color = 0xFFFDE68A.toInt()
        canvas.drawCircle(sx, headY, 4.4f * scale, paint)

        // Hat / Helmet / Hair
        paint.color = when {
            s.role == CampRole.SENTRY || s.role == CampRole.LEADER -> 0xFF334155.toInt()
            s.hasTrait(SurvivorTrait.MEDIC) -> Color.WHITE
            else -> 0xFF292524.toInt()
        }
        ovalRect.set(sx - 4.6f * scale, headY - 4.6f * scale, sx + 4.6f * scale, headY + 0.4f * scale)
        canvas.drawArc(ovalRect, 180f, 180f, true, paint)

        // Weapon & Arm animation
        val weapon = s.firearm?.takeIf { s.ammo > 0 && s.fsmState == FsmState.FIGHTING } ?: s.meleeWeapon
        val swingAngle = if (s.attackAnimTicks > 0 && !weapon.isFirearm) (5 - s.attackAnimTicks) * 0.45f else 0f
        val armAngle = kotlin.math.atan2(dirY, dirX) + swingAngle
        val handX = sx + cos(armAngle) * 7.5f * scale
        val handY = (sy - 12f * scale + crouch - bob) + sin(armAngle) * 5.5f * scale

        strokePaint.color = 0xFFFDE68A.toInt()
        strokePaint.strokeWidth = 2.4f * scale
        canvas.drawLine(sx + dirX * 2f * scale, sy - 13f * scale + crouch - bob, handX, handY, strokePaint)

        // Draw equipped weapon
        if (weapon != WeaponType.FISTS) {
            val wLen = when (weapon) {
                WeaponType.KNIFE -> 5f
                WeaponType.MACHETE -> 8.5f
                WeaponType.PISTOL -> 5.5f
                WeaponType.CROSSBOW -> 9.0f
                WeaponType.BAT, WeaponType.AXE -> 9.5f
                WeaponType.SHOTGUN, WeaponType.RIFLE -> 11.5f
                else -> 0f
            } * scale
            strokePaint.color = when (weapon) {
                WeaponType.BAT -> 0xFFB45309.toInt()
                WeaponType.CROSSBOW -> 0xFF92400E.toInt()
                WeaponType.AXE, WeaponType.MACHETE -> 0xFFCBD5E1.toInt()
                WeaponType.PISTOL, WeaponType.SHOTGUN, WeaponType.RIFLE -> 0xFF1E293B.toInt()
                else -> 0xFFE2E8F0.toInt()
            }
            strokePaint.strokeWidth = (if (weapon.isFirearm) 2.5f else 2.2f) * scale
            val wx2 = handX + cos(armAngle) * wLen
            val wy2 = handY + sin(armAngle) * wLen
            canvas.drawLine(handX, handY, wx2, wy2, strokePaint)
            if (weapon == WeaponType.CROSSBOW) {
                strokePaint.color = 0xFF475569.toInt()
                strokePaint.strokeWidth = 1.8f * scale
                val perpX = -sin(armAngle) * 4.5f * scale
                val perpY = cos(armAngle) * 4.5f * scale
                canvas.drawLine(wx2 - perpX, wy2 - perpY, wx2 + perpX, wy2 + perpY, strokePaint)
            }

            // Muzzle flash or slash arc!
            if (s.attackAnimTicks >= 4) {
                if (weapon.isFirearm) {
                    paint.color = 0xFFFDE047.toInt()
                    canvas.drawCircle(wx2, wy2, 4.5f * scale, paint)
                    paint.color = 0xFFF97316.toInt()
                    canvas.drawCircle(wx2, wy2, 2.5f * scale, paint)
                } else {
                    strokePaint.color = 0xAAFFFFFF.toInt()
                    strokePaint.strokeWidth = 2f * scale
                    canvas.drawLine(handX, handY, wx2 + 4f * scale, wy2 - 4f * scale, strokePaint)
                }
            }
        }

        // HP bar if injured
        if (s.hp < 95f && scale >= 0.65f) {
            val bw = 16f * scale
            paint.color = 0xAA0F172A.toInt()
            canvas.drawRect(sx - bw * 0.5f, headY - 9f * scale, sx + bw * 0.5f, headY - 6.5f * scale, paint)
            paint.color = if (s.hp > 50f) 0xFF10B981.toInt() else 0xFFEF4444.toInt()
            canvas.drawRect(sx - bw * 0.5f, headY - 9f * scale, sx - bw * 0.5f + bw * (s.hp / 100f).coerceIn(0f, 1f), headY - 6.5f * scale, paint)
        }

        // Overhead name, action tag & Russian thought bubble
        if (scale >= 0.72f) {
            textPaint.textSize = (11f * scale).coerceIn(17f, 26f)
            textPaint.color = Color.WHITE
            val labelY = headY - 12f * scale
            canvas.drawText("${s.name} [${s.currentActionIcon}]", sx, labelY, textPaint)

            if (scale >= 0.95f && s.thoughtTimer > 0 && s.thoughtText.isNotEmpty()) {
                textPaint.textSize = (10f * scale).coerceIn(17f, 22f)
                val tw = textPaint.measureText(s.thoughtText)
                ovalRect.set(sx - tw * 0.5f - 10f, labelY - 36f, sx + tw * 0.5f + 10f, labelY - 10f)
                canvas.drawRoundRect(ovalRect, 8f, 8f, bubblePaint)
                textPaint.color = 0xFFE2E8F0.toInt()
                canvas.drawText(s.thoughtText, sx, labelY - 17f, textPaint)
            }
        }
    }

    fun drawZombie(
        canvas: Canvas,
        z: ZombieAgent,
        sx: Float,
        sy: Float,
        scale: Float,
        renderAlpha: Float,
        selected: Boolean
    ) {
        val bulk = when (z.type) {
            ZombieType.BRUTE -> 1.38f
            ZombieType.ARMORED -> 1.15f
            ZombieType.RUNNER -> 0.96f
            else -> 1.0f
        } * scale

        val isoFx = z.facingX - z.facingY
        val isoFy = (z.facingX + z.facingY) * 0.5f
        val fl = kotlin.math.sqrt(isoFx * isoFx + isoFy * isoFy).coerceAtLeast(0.01f)
        val dirX = isoFx / fl
        val dirY = isoFy / fl

        // Ground shadow
        paint.color = 0x66000000
        ovalRect.set(sx - 9f * bulk, sy - 4f * bulk, sx + 9f * bulk, sy + 5f * bulk)
        canvas.drawOval(ovalRect, paint)

        if (selected) {
            strokePaint.color = Color.WHITE
            strokePaint.strokeWidth = 2.2f * scale.coerceAtLeast(0.6f)
            ovalRect.set(sx - 12f * bulk, sy - 6f * bulk, sx + 12f * bulk, sy + 6f * bulk)
            canvas.drawOval(ovalRect, strokePaint)
        }

        // Screamer eerie aura
        if (z.type == ZombieType.SCREAMER) {
            paint.color = 0x33A855F7
            canvas.drawCircle(sx, sy - 10f * bulk, 13f * bulk, paint)
        }

        val smoothPhase = z.animPhase + renderAlpha * 0.45f
        val sway = sin(smoothPhase) * 3.5f * bulk
        val lunge = if (z.attackAnimTicks > 0) (5 - z.attackAnimTicks) * 1.1f * bulk else 0f

        // Legs
        strokePaint.color = 0xFF1F2937.toInt()
        strokePaint.strokeWidth = 3f * bulk
        canvas.drawLine(sx - 2.4f * bulk, sy - 5f * bulk, sx - 2.4f * bulk - sway * 0.5f, sy + sway * 0.35f, strokePaint)
        canvas.drawLine(sx + 2.4f * bulk, sy - 5f * bulk, sx + 2.4f * bulk + sway * 0.5f, sy - sway * 0.35f, strokePaint)

        // Torso
        paint.color = when {
            z.hurtAnimTicks > 0 -> Color.WHITE
            z.type == ZombieType.WALKER -> 0xFF4D7C0F.toInt()
            z.type == ZombieType.RUNNER -> 0xFFB91C1C.toInt()
            z.type == ZombieType.SCREAMER -> 0xFF7E22CE.toInt()
            z.type == ZombieType.ARMORED -> 0xFF1E293B.toInt()
            else -> 0xFF7F1D1D.toInt()
        }
        ovalRect.set(sx - 5.5f * bulk, sy - 16f * bulk, sx + 5.5f * bulk, sy - 4f * bulk)
        canvas.drawRoundRect(ovalRect, 3f * bulk, 3f * bulk, paint)
        if (z.type == ZombieType.ARMORED) {
            paint.color = 0xFF475569.toInt()
            ovalRect.set(sx - 4.5f * bulk, sy - 15f * bulk, sx + 4.5f * bulk, sy - 7f * bulk)
            canvas.drawRoundRect(ovalRect, 2f * bulk, 2f * bulk, paint)
        }

        // Outstretched zombie arms
        strokePaint.color = when (z.type) {
            ZombieType.RUNNER -> 0xFFFCA5A5.toInt()
            ZombieType.SCREAMER -> 0xFFD8B4FE.toInt()
            ZombieType.ARMORED -> 0xFF64748B.toInt()
            else -> 0xFF84CC16.toInt()
        }
        strokePaint.strokeWidth = 2.6f * bulk
        val reach = (7.5f * bulk) + lunge
        canvas.drawLine(sx - 3f * bulk, sy - 12f * bulk, sx - 3f * bulk + dirX * reach, sy - 12f * bulk + dirY * reach, strokePaint)
        canvas.drawLine(sx + 3f * bulk, sy - 12f * bulk, sx + 3f * bulk + dirX * reach, sy - 12f * bulk + dirY * reach, strokePaint)

        // Head (tilted shambling)
        paint.color = if (z.hurtAnimTicks > 0) Color.WHITE else when (z.type) {
            ZombieType.SCREAMER -> 0xFFC084FC.toInt()
            ZombieType.RUNNER -> 0xFFF87171.toInt()
            ZombieType.ARMORED -> 0xFF334155.toInt()
            else -> 0xFFA3E635.toInt()
        }
        canvas.drawCircle(sx + sway * 0.12f, sy - 19f * bulk, 4.2f * bulk, paint)
        if (z.type == ZombieType.ARMORED) {
            paint.color = 0xFF0F172A.toInt()
            ovalRect.set(sx - 4.5f * bulk, sy - 23.5f * bulk, sx + 4.5f * bulk, sy - 18.5f * bulk)
            canvas.drawArc(ovalRect, 180f, 180f, true, paint)
        }

        // Glowing eyes
        paint.color = if (z.type == ZombieType.SCREAMER) 0xFFE879F9.toInt() else 0xFFEF4444.toInt()
        canvas.drawCircle(sx + dirX * 2f * bulk, sy - 19f * bulk + dirY * 1f * bulk, 1.3f * bulk, paint)
    }
}
