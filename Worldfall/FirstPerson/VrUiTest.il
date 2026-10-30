.class private auto ansi sealed beforefieldinit FirstPerson.VrUiTest
	extends FirstPerson.CivilTest
{
	// Nested Types
	.class nested private auto ansi sealed beforefieldinit '<Body>d__6'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private class ['Assembly-CSharp']Actor '<king>5__2'
		.field private class FirstPerson.Settings '<s>5__3'
		.field private bool '<savedVr>5__4'
		.field private bool '<savedGod>5__5'
		.field private bool '<savedGpu>5__6'
		.field private int32 '<savedTurn>5__7'
		.field private int32 '<savedDive>5__8'
		.field private int32 '<savedTime>5__9'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51c768
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
			IL_000d: ret
		} // end of method '<Body>d__6'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51c778
			// Header size: 12
			// Code size: 29 (0x1d)
			.maxstack 2
			.locals init (
				[0] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldloc.0
			IL_0008: ldc.i4.s -3
			IL_000a: beq.s IL_0012

			IL_000c: ldloc.0
			IL_000d: ldc.i4.2
			IL_000e: sub
			IL_000f: ldc.i4.8
			IL_0010: bgt.un.s IL_001c

			IL_0012: nop
			.try
			{
				IL_0013: leave.s IL_001c
			} // end .try
			finally
			{
				IL_0015: ldarg.0
				IL_0016: call instance void FirstPerson.VrUiTest/'<Body>d__6'::'<>m__Finally1'()
				IL_001b: endfinally
			} // end handler

			IL_001c: ret
		} // end of method '<Body>d__6'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51c7b4
			// Header size: 12
			// Code size: 961 (0x3c1)
			.maxstack 8
			.locals init (
				[0] bool,
				[1] int32,
				[2] class FirstPerson.VrUiTest
			)

			.try
			{
				IL_0000: ldarg.0
				IL_0001: ldfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0006: stloc.1
				IL_0007: ldarg.0
				IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Body>d__6'::'<>4__this'
				IL_000d: stloc.2
				IL_000e: ldloc.1
				IL_000f: switch (IL_0047, IL_00a3, IL_01e6, IL_0262, IL_028f, IL_02bc, IL_02e9, IL_0316, IL_0340, IL_036b, IL_039c)

				IL_0040: ldc.i4.0
				IL_0041: stloc.0
				IL_0042: leave IL_03bf

				IL_0047: ldarg.0
				IL_0048: ldc.i4.m1
				IL_0049: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_004e: ldarg.0
				IL_004f: ldloc.2
				IL_0050: ldstr "human"
				IL_0055: ldc.i4.6
				IL_0056: ldc.i4.0
				IL_0057: ldnull
				IL_0058: ldc.r4 0.0
				IL_005d: ldnull
				IL_005e: call instance class ['Assembly-CSharp']Actor FirstPerson.CivilTest::Village(string, int32, int32, class ['Assembly-CSharp']WorldTile, float32, class ['Assembly-CSharp']WorldTile)
				IL_0063: stfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Body>d__6'::'<king>5__2'
				IL_0068: ldarg.0
				IL_0069: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Body>d__6'::'<king>5__2'
				IL_006e: brtrue.s IL_0085

				IL_0070: ldloc.2
				IL_0071: ldstr "a village was founded"
				IL_0076: ldc.i4.0
				IL_0077: ldnull
				IL_0078: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
				IL_007d: pop
				IL_007e: ldc.i4.0
				IL_007f: stloc.0
				IL_0080: leave IL_03bf

				IL_0085: ldarg.0
				IL_0086: ldc.r4 4
				IL_008b: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
				IL_0090: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_0095: ldarg.0
				IL_0096: ldc.i4.1
				IL_0097: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_009c: ldc.i4.1
				IL_009d: stloc.0
				IL_009e: leave IL_03bf

				IL_00a3: ldarg.0
				IL_00a4: ldc.i4.m1
				IL_00a5: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				.try
				{
					IL_00aa: ldc.i4.0
					IL_00ab: call void ['Assembly-CSharp']ScrollWindow::hideAllEvent(bool)
					IL_00b0: leave.s IL_00b5
				} // end .try
				catch [mscorlib]System.Object
				{
					IL_00b2: pop
					IL_00b3: leave.s IL_00b5
				} // end handler

				IL_00b5: ldloc.2
				IL_00b6: ldarg.0
				IL_00b7: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Body>d__6'::'<king>5__2'
				IL_00bc: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']BaseSimObject::current_position
				IL_00c1: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
				IL_00c6: ldarg.0
				IL_00c7: ldloc.2
				IL_00c8: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
				IL_00cd: ldfld class FirstPerson.Settings FirstPerson.WorldBoxMod::Settings
				IL_00d2: stfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_00d7: ldarg.0
				IL_00d8: ldarg.0
				IL_00d9: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_00de: ldfld bool FirstPerson.Settings::Vr
				IL_00e3: stfld bool FirstPerson.VrUiTest/'<Body>d__6'::'<savedVr>5__4'
				IL_00e8: ldarg.0
				IL_00e9: ldarg.0
				IL_00ea: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_00ef: ldfld bool FirstPerson.Settings::GodView
				IL_00f4: stfld bool FirstPerson.VrUiTest/'<Body>d__6'::'<savedGod>5__5'
				IL_00f9: ldarg.0
				IL_00fa: ldarg.0
				IL_00fb: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_0100: ldfld bool FirstPerson.Settings::GpuRender
				IL_0105: stfld bool FirstPerson.VrUiTest/'<Body>d__6'::'<savedGpu>5__6'
				IL_010a: ldarg.0
				IL_010b: ldarg.0
				IL_010c: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_0111: ldfld int32 FirstPerson.Settings::VrTurn
				IL_0116: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<savedTurn>5__7'
				IL_011b: ldarg.0
				IL_011c: ldarg.0
				IL_011d: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_0122: ldfld int32 FirstPerson.Settings::VrDive
				IL_0127: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<savedDive>5__8'
				IL_012c: ldarg.0
				IL_012d: ldarg.0
				IL_012e: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_0133: ldfld int32 FirstPerson.Settings::TimeMode
				IL_0138: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<savedTime>5__9'
				IL_013d: ldc.i4.1
				IL_013e: stsfld bool FirstPerson.WorldBoxMod::VrSimulate
				IL_0143: ldloc.2
				IL_0144: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
				IL_0149: callvirt instance void FirstPerson.WorldBoxMod::VrResetForTest()
				IL_014e: ldloc.2
				IL_014f: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
				IL_0154: ldc.i4.1
				IL_0155: stfld bool FirstPerson.WorldBoxMod::MouseLookOff
				IL_015a: ldc.i4.1
				IL_015b: stsfld bool FirstPerson.Vr.VrMouse::IgnoreRealMouse
				IL_0160: ldloc.2
				IL_0161: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
				IL_0166: ldc.i4.0
				IL_0167: callvirt instance void FirstPerson.WorldBoxMod::set_NoDive(bool)
				IL_016c: ldarg.0
				IL_016d: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_0172: ldc.i4.1
				IL_0173: stfld bool FirstPerson.Settings::Vr
				IL_0178: ldarg.0
				IL_0179: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_017e: ldc.i4.0
				IL_017f: stfld int32 FirstPerson.Settings::VrTurn
				IL_0184: ldarg.0
				IL_0185: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_018a: ldc.i4.0
				IL_018b: stfld int32 FirstPerson.Settings::VrDive
				IL_0190: ldarg.0
				IL_0191: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_0196: ldc.i4.1
				IL_0197: stfld bool FirstPerson.Settings::GpuRender
				IL_019c: ldarg.0
				IL_019d: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_01a2: ldc.i4.1
				IL_01a3: stfld bool FirstPerson.Settings::GodView
				IL_01a8: ldarg.0
				IL_01a9: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
				IL_01ae: ldc.i4.1
				IL_01af: stfld int32 FirstPerson.Settings::TimeMode
				IL_01b4: ldarg.0
				IL_01b5: ldc.i4.s -3
				IL_01b7: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_01bc: ldarg.0
				IL_01bd: ldloc.2
				IL_01be: ldloc.2
				IL_01bf: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
				IL_01c4: ldc.r4 20
				IL_01c9: ldc.r4 2
				IL_01ce: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
				IL_01d3: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_01d8: ldarg.0
				IL_01d9: ldc.i4.2
				IL_01da: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_01df: ldc.i4.1
				IL_01e0: stloc.0
				IL_01e1: leave IL_03bf

				IL_01e6: ldarg.0
				IL_01e7: ldc.i4.s -3
				IL_01e9: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_01ee: ldloc.2
				IL_01ef: ldstr "the pretend headset runs"
				IL_01f4: ldloc.2
				IL_01f5: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
				IL_01fa: brfalse.s IL_0216

				IL_01fc: ldloc.2
				IL_01fd: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
				IL_0202: ldfld bool FirstPerson.Vr.VrSystem::Simulated
				IL_0207: brfalse.s IL_0216

				IL_0209: ldloc.2
				IL_020a: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
				IL_020f: callvirt instance bool FirstPerson.WorldBoxMod::get_VrRunning()
				IL_0214: br.s IL_0217

				IL_0216: ldc.i4.0

				IL_0217: ldloc.2
				IL_0218: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
				IL_021d: dup
				IL_021e: brtrue.s IL_0224

				IL_0220: pop
				IL_0221: ldnull
				IL_0222: br.s IL_0229

				IL_0224: ldfld string FirstPerson.Vr.VrSystem::Status

				IL_0229: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
				IL_022e: brtrue.s IL_0237

				IL_0230: ldc.i4.0
				IL_0231: stloc.0
				IL_0232: br IL_03ac

				IL_0237: ldloc.2
				IL_0238: call instance void FirstPerson.VrUiTest::MakeImages()
				IL_023d: ldarg.0
				IL_023e: ldloc.2
				IL_023f: ldstr "the god view in the headset"
				IL_0244: ldloc.2
				IL_0245: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::GodEyes()
				IL_024a: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_024f: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_0254: ldarg.0
				IL_0255: ldc.i4.3
				IL_0256: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_025b: ldc.i4.1
				IL_025c: stloc.0
				IL_025d: leave IL_03bf

				IL_0262: ldarg.0
				IL_0263: ldc.i4.s -3
				IL_0265: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_026a: ldarg.0
				IL_026b: ldloc.2
				IL_026c: ldstr "looking round"
				IL_0271: ldloc.2
				IL_0272: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Looks()
				IL_0277: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_027c: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_0281: ldarg.0
				IL_0282: ldc.i4.4
				IL_0283: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0288: ldc.i4.1
				IL_0289: stloc.0
				IL_028a: leave IL_03bf

				IL_028f: ldarg.0
				IL_0290: ldc.i4.s -3
				IL_0292: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0297: ldarg.0
				IL_0298: ldloc.2
				IL_0299: ldstr "the laser on the land"
				IL_029e: ldloc.2
				IL_029f: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::LaserLand()
				IL_02a4: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_02a9: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_02ae: ldarg.0
				IL_02af: ldc.i4.5
				IL_02b0: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_02b5: ldc.i4.1
				IL_02b6: stloc.0
				IL_02b7: leave IL_03bf

				IL_02bc: ldarg.0
				IL_02bd: ldc.i4.s -3
				IL_02bf: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_02c4: ldarg.0
				IL_02c5: ldloc.2
				IL_02c6: ldstr "a power where the laser points"
				IL_02cb: ldloc.2
				IL_02cc: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::LaserPower()
				IL_02d1: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_02d6: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_02db: ldarg.0
				IL_02dc: ldc.i4.6
				IL_02dd: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_02e2: ldc.i4.1
				IL_02e3: stloc.0
				IL_02e4: leave IL_03bf

				IL_02e9: ldarg.0
				IL_02ea: ldc.i4.s -3
				IL_02ec: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_02f1: ldarg.0
				IL_02f2: ldloc.2
				IL_02f3: ldstr "the game's buttons on the desk"
				IL_02f8: ldloc.2
				IL_02f9: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::LaserButton()
				IL_02fe: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_0303: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_0308: ldarg.0
				IL_0309: ldc.i4.7
				IL_030a: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_030f: ldc.i4.1
				IL_0310: stloc.0
				IL_0311: leave IL_03bf

				IL_0316: ldarg.0
				IL_0317: ldc.i4.s -3
				IL_0319: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_031e: ldarg.0
				IL_031f: ldloc.2
				IL_0320: ldstr "the sticks and a grip"
				IL_0325: ldloc.2
				IL_0326: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Sticks()
				IL_032b: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_0330: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_0335: ldarg.0
				IL_0336: ldc.i4.8
				IL_0337: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_033c: ldc.i4.1
				IL_033d: stloc.0
				IL_033e: leave.s IL_03bf

				IL_0340: ldarg.0
				IL_0341: ldc.i4.s -3
				IL_0343: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0348: ldarg.0
				IL_0349: ldloc.2
				IL_034a: ldstr "Y: the flat map and back"
				IL_034f: ldloc.2
				IL_0350: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::FlatAndBack()
				IL_0355: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_035a: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_035f: ldarg.0
				IL_0360: ldc.i4.s 9
				IL_0362: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0367: ldc.i4.1
				IL_0368: stloc.0
				IL_0369: leave.s IL_03bf

				IL_036b: ldarg.0
				IL_036c: ldc.i4.s -3
				IL_036e: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0373: ldarg.0
				IL_0374: ldloc.2
				IL_0375: ldstr "windows in first person"
				IL_037a: ldloc.2
				IL_037b: ldarg.0
				IL_037c: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Body>d__6'::'<king>5__2'
				IL_0381: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Windows(class ['Assembly-CSharp']Actor)
				IL_0386: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.CivilTest::Part(string, class [mscorlib]System.Collections.IEnumerator)
				IL_038b: stfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
				IL_0390: ldarg.0
				IL_0391: ldc.i4.s 10
				IL_0393: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_0398: ldc.i4.1
				IL_0399: stloc.0
				IL_039a: leave.s IL_03bf

				IL_039c: ldarg.0
				IL_039d: ldc.i4.s -3
				IL_039f: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
				IL_03a4: ldarg.0
				IL_03a5: call instance void FirstPerson.VrUiTest/'<Body>d__6'::'<>m__Finally1'()
				IL_03aa: br.s IL_03b4

				IL_03ac: ldarg.0
				IL_03ad: call instance void FirstPerson.VrUiTest/'<Body>d__6'::'<>m__Finally1'()
				IL_03b2: leave.s IL_03bf

				IL_03b4: ldc.i4.0
				IL_03b5: stloc.0
				IL_03b6: leave.s IL_03bf
			} // end .try
			fault
			{
				IL_03b8: ldarg.0
				IL_03b9: call instance void FirstPerson.VrUiTest/'<Body>d__6'::System.IDisposable.Dispose()
				IL_03be: endfinally
			} // end handler

			IL_03bf: ldloc.0
			IL_03c0: ret
		} // end of method '<Body>d__6'::MoveNext

		.method private hidebysig 
			instance void '<>m__Finally1' () cil managed 
		{
			// Method begins at RVA 0x51cbb8
			// Header size: 12
			// Code size: 355 (0x163)
			.maxstack 6
			.locals init (
				[0] class FirstPerson.VrUiTest,
				[1] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[2] float32,
				[3] bool,
				[4] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldc.i4.m1
			IL_0002: stfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<>1__state'
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Body>d__6'::'<>4__this'
			IL_000d: stloc.0
			IL_000e: ldarg.0
			IL_000f: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_0014: ldarg.0
			IL_0015: ldfld bool FirstPerson.VrUiTest/'<Body>d__6'::'<savedVr>5__4'
			IL_001a: stfld bool FirstPerson.Settings::Vr
			IL_001f: ldarg.0
			IL_0020: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_0025: ldarg.0
			IL_0026: ldfld bool FirstPerson.VrUiTest/'<Body>d__6'::'<savedGod>5__5'
			IL_002b: stfld bool FirstPerson.Settings::GodView
			IL_0030: ldarg.0
			IL_0031: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_0036: ldarg.0
			IL_0037: ldfld bool FirstPerson.VrUiTest/'<Body>d__6'::'<savedGpu>5__6'
			IL_003c: stfld bool FirstPerson.Settings::GpuRender
			IL_0041: ldarg.0
			IL_0042: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_0047: ldarg.0
			IL_0048: ldfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<savedTurn>5__7'
			IL_004d: stfld int32 FirstPerson.Settings::VrTurn
			IL_0052: ldarg.0
			IL_0053: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_0058: ldarg.0
			IL_0059: ldfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<savedDive>5__8'
			IL_005e: stfld int32 FirstPerson.Settings::VrDive
			IL_0063: ldarg.0
			IL_0064: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_0069: ldarg.0
			IL_006a: ldfld int32 FirstPerson.VrUiTest/'<Body>d__6'::'<savedTime>5__9'
			IL_006f: stfld int32 FirstPerson.Settings::TimeMode
			IL_0074: ldloc.0
			IL_0075: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_007a: brfalse IL_0128

			IL_007f: ldloc.0
			IL_0080: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0085: call valuetype FirstPerson.Vr.Xr/Pose FirstPerson.Vr.Xr/Pose::get_Identity()
			IL_008a: stfld valuetype FirstPerson.Vr.Xr/Pose FirstPerson.Vr.VrSystem::SimHead
			IL_008f: ldloc.0
			IL_0090: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0095: ldloc.0
			IL_0096: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_009b: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_zero()
			IL_00a0: dup
			IL_00a1: stloc.1
			IL_00a2: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimMove
			IL_00a7: ldloc.1
			IL_00a8: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimTurn
			IL_00ad: ldloc.0
			IL_00ae: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00b3: ldloc.0
			IL_00b4: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00b9: ldloc.0
			IL_00ba: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00bf: ldloc.0
			IL_00c0: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00c5: ldc.r4 0.0
			IL_00ca: dup
			IL_00cb: stloc.2
			IL_00cc: stfld float32 FirstPerson.Vr.VrSystem::SimContext
			IL_00d1: ldloc.2
			IL_00d2: dup
			IL_00d3: stloc.2
			IL_00d4: stfld float32 FirstPerson.Vr.VrSystem::SimUse
			IL_00d9: ldloc.2
			IL_00da: dup
			IL_00db: stloc.2
			IL_00dc: stfld float32 FirstPerson.Vr.VrSystem::SimKick
			IL_00e1: ldloc.2
			IL_00e2: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_00e7: ldloc.0
			IL_00e8: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00ed: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
			IL_00f2: ldc.i4.0
			IL_00f3: ldloc.0
			IL_00f4: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00f9: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
			IL_00fe: ldc.i4.1
			IL_00ff: ldc.i4.0
			IL_0100: dup
			IL_0101: stloc.3
			IL_0102: stelem.i1
			IL_0103: ldloc.3
			IL_0104: stelem.i1
			IL_0105: ldc.i4.0
			IL_0106: stloc.s 4
			IL_0108: br.s IL_0123
			// loop start (head: IL_0123)
				IL_010a: ldloc.0
				IL_010b: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
				IL_0110: ldfld native int[] FirstPerson.Vr.VrSystem::SimImages
				IL_0115: ldloc.s 4
				IL_0117: ldsfld native int [mscorlib]System.IntPtr::Zero
				IL_011c: stelem.i
				IL_011d: ldloc.s 4
				IL_011f: ldc.i4.1
				IL_0120: add
				IL_0121: stloc.s 4

				IL_0123: ldloc.s 4
				IL_0125: ldc.i4.3
				IL_0126: blt.s IL_010a
			// end loop

			IL_0128: ldc.i4.0
			IL_0129: stsfld bool FirstPerson.WorldBoxMod::VrSimulate
			IL_012e: ldloc.0
			IL_012f: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0134: ldc.i4.0
			IL_0135: stfld bool FirstPerson.WorldBoxMod::MouseLookOff
			IL_013a: ldc.i4.0
			IL_013b: stsfld bool FirstPerson.Vr.VrMouse::IgnoreRealMouse
			IL_0140: ldloc.0
			IL_0141: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0146: ldc.i4.1
			IL_0147: callvirt instance void FirstPerson.WorldBoxMod::set_NoDive(bool)
			IL_014c: ldloc.0
			IL_014d: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0152: callvirt instance void FirstPerson.WorldBoxMod::VrResetForTest()
			IL_0157: ldarg.0
			IL_0158: ldfld class FirstPerson.Settings FirstPerson.VrUiTest/'<Body>d__6'::'<s>5__3'
			IL_015d: callvirt instance void FirstPerson.Settings::Save()
			IL_0162: ret
		} // end of method '<Body>d__6'::'<>m__Finally1'

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51cd27
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
			IL_0006: ret
		} // end of method '<Body>d__6'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51cd2f
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<Body>d__6'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51cd36
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Body>d__6'::'<>2__current'
			IL_0006: ret
		} // end of method '<Body>d__6'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<Body>d__6'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<Body>d__6'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <Body>d__6

	.class nested private auto ansi sealed beforefieldinit '<FlatAndBack>d__16'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private float32 '<first>5__2'
		.field private float32 '<fade>5__3'
		.field private int32 '<i>5__4'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51cd3e
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_000d: ret
		} // end of method '<FlatAndBack>d__16'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51cd4d
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<FlatAndBack>d__16'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51cd50
			// Header size: 12
			// Code size: 796 (0x31c)
			.maxstack 6
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] class FirstPerson.Vr.VrSystem/Layers,
				[3] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_003a, IL_005d, IL_0074, IL_00ab, IL_015a, IL_01b7, IL_025c, IL_0273, IL_02ab)

			IL_0038: ldc.i4.0
			IL_0039: ret

			IL_003a: ldarg.0
			IL_003b: ldc.i4.m1
			IL_003c: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0041: ldloc.1
			IL_0042: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0047: ldc.i4.1
			IL_0048: stfld bool FirstPerson.Vr.VrSystem::SimMap
			IL_004d: ldarg.0
			IL_004e: ldnull
			IL_004f: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_0054: ldarg.0
			IL_0055: ldc.i4.1
			IL_0056: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_005b: ldc.i4.1
			IL_005c: ret

			IL_005d: ldarg.0
			IL_005e: ldc.i4.m1
			IL_005f: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0064: ldarg.0
			IL_0065: ldnull
			IL_0066: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_006b: ldarg.0
			IL_006c: ldc.i4.2
			IL_006d: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0072: ldc.i4.1
			IL_0073: ret

			IL_0074: ldarg.0
			IL_0075: ldc.i4.m1
			IL_0076: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_007b: ldloc.1
			IL_007c: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0081: ldc.i4.0
			IL_0082: stfld bool FirstPerson.Vr.VrSystem::SimMap
			IL_0087: ldarg.0
			IL_0088: ldc.r4 -1
			IL_008d: stfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<first>5__2'
			IL_0092: ldarg.0
			IL_0093: ldc.i4.0
			IL_0094: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<i>5__4'
			IL_0099: br.s IL_00fc

			IL_009b: ldarg.0
			IL_009c: ldnull
			IL_009d: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_00a2: ldarg.0
			IL_00a3: ldc.i4.3
			IL_00a4: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_00a9: ldc.i4.1
			IL_00aa: ret

			IL_00ab: ldarg.0
			IL_00ac: ldc.i4.m1
			IL_00ad: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_00b2: ldloc.1
			IL_00b3: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00b8: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_00bd: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_00c2: brtrue.s IL_00ec

			IL_00c4: ldloc.1
			IL_00c5: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00ca: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_00cf: ldfld bool FirstPerson.Vr.VrSystem/Layers::Panel
			IL_00d4: brfalse.s IL_00ec

			IL_00d6: ldarg.0
			IL_00d7: ldloc.1
			IL_00d8: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00dd: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_00e2: ldfld float32 FirstPerson.Vr.VrSystem/Layers::Fade
			IL_00e7: stfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<first>5__2'

			IL_00ec: ldarg.0
			IL_00ed: ldfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<i>5__4'
			IL_00f2: stloc.3
			IL_00f3: ldarg.0
			IL_00f4: ldloc.3
			IL_00f5: ldc.i4.1
			IL_00f6: add
			IL_00f7: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<i>5__4'

			IL_00fc: ldarg.0
			IL_00fd: ldfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<i>5__4'
			IL_0102: ldc.i4.s 60
			IL_0104: bge.s IL_0113

			IL_0106: ldarg.0
			IL_0107: ldfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<first>5__2'
			IL_010c: ldc.r4 0.0
			IL_0111: blt.s IL_009b

			IL_0113: ldloc.1
			IL_0114: ldstr "the flat screen's first frame is dark (the dip starts with it)"
			IL_0119: ldarg.0
			IL_011a: ldfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<first>5__2'
			IL_011f: ldc.r4 0.85
			IL_0124: cgt
			IL_0126: ldstr "fade {0:F2}"
			IL_012b: ldarg.0
			IL_012c: ldfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<first>5__2'
			IL_0131: box [mscorlib]System.Single
			IL_0136: call string [mscorlib]System.String::Format(string, object)
			IL_013b: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0140: pop
			IL_0141: ldarg.0
			IL_0142: ldc.r4 0.1
			IL_0147: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_014c: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_0151: ldarg.0
			IL_0152: ldc.i4.4
			IL_0153: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0158: ldc.i4.1
			IL_0159: ret

			IL_015a: ldarg.0
			IL_015b: ldc.i4.m1
			IL_015c: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0161: ldloc.1
			IL_0162: ldstr "Y switches to the flat map"
			IL_0167: ldloc.1
			IL_0168: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_016d: ldfld class FirstPerson.Settings FirstPerson.WorldBoxMod::Settings
			IL_0172: ldfld bool FirstPerson.Settings::GodView
			IL_0177: ldc.i4.0
			IL_0178: ceq
			IL_017a: ldnull
			IL_017b: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0180: pop
			IL_0181: ldarg.0
			IL_0182: ldloc.1
			IL_0183: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0188: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrFadeForTest()
			IL_018d: stfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<fade>5__3'
			IL_0192: ldarg.0
			IL_0193: ldloc.1
			IL_0194: ldloc.1
			IL_0195: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_019a: ldc.r4 20
			IL_019f: ldc.r4 0.8
			IL_01a4: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_01a9: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_01ae: ldarg.0
			IL_01af: ldc.i4.5
			IL_01b0: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_01b5: ldc.i4.1
			IL_01b6: ret

			IL_01b7: ldarg.0
			IL_01b8: ldc.i4.m1
			IL_01b9: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_01be: ldloc.1
			IL_01bf: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_01c4: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_01c9: stloc.2
			IL_01ca: ldloc.1
			IL_01cb: ldstr "the flat map: the game's screen hung in front"
			IL_01d0: ldloc.2
			IL_01d1: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_01d6: brtrue.s IL_01eb

			IL_01d8: ldloc.2
			IL_01d9: ldfld bool FirstPerson.Vr.VrSystem/Layers::Panel
			IL_01de: brfalse.s IL_01eb

			IL_01e0: ldloc.2
			IL_01e1: ldfld bool FirstPerson.Vr.VrSystem/Layers::PanelSeeThrough
			IL_01e6: ldc.i4.0
			IL_01e7: ceq
			IL_01e9: br.s IL_01ec

			IL_01eb: ldc.i4.0

			IL_01ec: ldstr "eyes {0}, see-through {1}"
			IL_01f1: ldloc.2
			IL_01f2: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_01f7: box [mscorlib]System.Int32
			IL_01fc: ldloc.2
			IL_01fd: ldfld bool FirstPerson.Vr.VrSystem/Layers::PanelSeeThrough
			IL_0202: box [mscorlib]System.Boolean
			IL_0207: call string [mscorlib]System.String::Format(string, object, object)
			IL_020c: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0211: pop
			IL_0212: ldloc.1
			IL_0213: ldstr "the switch dipped through black"
			IL_0218: ldarg.0
			IL_0219: ldfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<fade>5__3'
			IL_021e: ldc.r4 0.0
			IL_0223: cgt
			IL_0225: ldstr "fade {0:F2} just after"
			IL_022a: ldarg.0
			IL_022b: ldfld float32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<fade>5__3'
			IL_0230: box [mscorlib]System.Single
			IL_0235: call string [mscorlib]System.String::Format(string, object)
			IL_023a: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_023f: pop
			IL_0240: ldloc.1
			IL_0241: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0246: ldc.i4.1
			IL_0247: stfld bool FirstPerson.Vr.VrSystem::SimMap
			IL_024c: ldarg.0
			IL_024d: ldnull
			IL_024e: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_0253: ldarg.0
			IL_0254: ldc.i4.6
			IL_0255: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_025a: ldc.i4.1
			IL_025b: ret

			IL_025c: ldarg.0
			IL_025d: ldc.i4.m1
			IL_025e: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0263: ldarg.0
			IL_0264: ldnull
			IL_0265: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_026a: ldarg.0
			IL_026b: ldc.i4.7
			IL_026c: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_0271: ldc.i4.1
			IL_0272: ret

			IL_0273: ldarg.0
			IL_0274: ldc.i4.m1
			IL_0275: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_027a: ldloc.1
			IL_027b: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0280: ldc.i4.0
			IL_0281: stfld bool FirstPerson.Vr.VrSystem::SimMap
			IL_0286: ldarg.0
			IL_0287: ldloc.1
			IL_0288: ldloc.1
			IL_0289: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_028e: ldc.r4 20
			IL_0293: ldc.r4 1.2
			IL_0298: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_029d: stfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_02a2: ldarg.0
			IL_02a3: ldc.i4.8
			IL_02a4: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_02a9: ldc.i4.1
			IL_02aa: ret

			IL_02ab: ldarg.0
			IL_02ac: ldc.i4.m1
			IL_02ad: stfld int32 FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>1__state'
			IL_02b2: ldloc.1
			IL_02b3: ldstr "Y again: the 3D view for both eyes"
			IL_02b8: ldloc.1
			IL_02b9: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02be: ldfld class FirstPerson.Settings FirstPerson.WorldBoxMod::Settings
			IL_02c3: ldfld bool FirstPerson.Settings::GodView
			IL_02c8: brfalse.s IL_02df

			IL_02ca: ldloc.1
			IL_02cb: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_02d0: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_02d5: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_02da: ldc.i4.2
			IL_02db: ceq
			IL_02dd: br.s IL_02e0

			IL_02df: ldc.i4.0

			IL_02e0: ldstr "god view {0}, eyes {1}"
			IL_02e5: ldloc.1
			IL_02e6: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02eb: ldfld class FirstPerson.Settings FirstPerson.WorldBoxMod::Settings
			IL_02f0: ldfld bool FirstPerson.Settings::GodView
			IL_02f5: box [mscorlib]System.Boolean
			IL_02fa: ldloc.1
			IL_02fb: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0300: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_0305: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_030a: box [mscorlib]System.Int32
			IL_030f: call string [mscorlib]System.String::Format(string, object, object)
			IL_0314: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0319: pop
			IL_031a: ldc.i4.0
			IL_031b: ret
		} // end of method '<FlatAndBack>d__16'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51d078
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_0006: ret
		} // end of method '<FlatAndBack>d__16'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51d080
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<FlatAndBack>d__16'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51d087
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>2__current'
			IL_0006: ret
		} // end of method '<FlatAndBack>d__16'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<FlatAndBack>d__16'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <FlatAndBack>d__16

	.class nested private auto ansi sealed beforefieldinit '<GodEyes>d__8'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private int32 '<before>5__2'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51d08f
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_000d: ret
		} // end of method '<GodEyes>d__8'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51d09e
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<GodEyes>d__8'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51d0a0
			// Header size: 12
			// Code size: 1272 (0x4f8)
			.maxstack 8
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] class FirstPerson.Vr.VrSystem/Layers,
				[3] float32,
				[4] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
				[5] float32,
				[6] float32,
				[7] float32,
				[8] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[9] valuetype FirstPerson.Gpu.EyeCamera,
				[10] float32,
				[11] class [UnityEngine.CoreModule]UnityEngine.Camera,
				[12] class [UnityEngine.CoreModule]UnityEngine.RenderTexture,
				[13] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
				[14] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
				[15] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
				[16] int32,
				[17] int32,
				[18] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
				[19] int32,
				[20] valuetype [UnityEngine.CoreModule]UnityEngine.Color32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_0022, IL_007c, IL_00b9)

			IL_0020: ldc.i4.0
			IL_0021: ret

			IL_0022: ldarg.0
			IL_0023: ldc.i4.m1
			IL_0024: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_0029: ldloc.1
			IL_002a: ldc.r4 0.0
			IL_002f: ldc.r4 1
			IL_0034: ldc.r4 0.0
			IL_0039: ldc.r4 0.0
			IL_003e: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_0043: ldc.r4 0.0
			IL_0048: ldc.r4 0.0
			IL_004d: ldc.r4 0.0
			IL_0052: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_0057: ldarg.0
			IL_0058: ldloc.1
			IL_0059: ldloc.1
			IL_005a: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_005f: ldc.r4 20
			IL_0064: ldc.r4 2
			IL_0069: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_006e: stfld object FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>2__current'
			IL_0073: ldarg.0
			IL_0074: ldc.i4.1
			IL_0075: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_007a: ldc.i4.1
			IL_007b: ret

			IL_007c: ldarg.0
			IL_007d: ldc.i4.m1
			IL_007e: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_0083: ldarg.0
			IL_0084: ldloc.1
			IL_0085: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_008a: ldfld int32 FirstPerson.WorldBoxMod::VrGodFrames
			IL_008f: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<before>5__2'
			IL_0094: ldarg.0
			IL_0095: ldloc.1
			IL_0096: ldloc.1
			IL_0097: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_009c: ldc.r4 20
			IL_00a1: ldc.r4 1.5
			IL_00a6: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_00ab: stfld object FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>2__current'
			IL_00b0: ldarg.0
			IL_00b1: ldc.i4.2
			IL_00b2: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_00b7: ldc.i4.1
			IL_00b8: ret

			IL_00b9: ldarg.0
			IL_00ba: ldc.i4.m1
			IL_00bb: stfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>1__state'
			IL_00c0: ldloc.1
			IL_00c1: ldstr "the god view is drawn for both eyes every frame"
			IL_00c6: ldloc.1
			IL_00c7: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_00cc: ldfld int32 FirstPerson.WorldBoxMod::VrGodFrames
			IL_00d1: ldarg.0
			IL_00d2: ldfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<before>5__2'
			IL_00d7: sub
			IL_00d8: ldc.i4.s 10
			IL_00da: cgt
			IL_00dc: ldstr "{0} frames in 1.5 s"
			IL_00e1: ldloc.1
			IL_00e2: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_00e7: ldfld int32 FirstPerson.WorldBoxMod::VrGodFrames
			IL_00ec: ldarg.0
			IL_00ed: ldfld int32 FirstPerson.VrUiTest/'<GodEyes>d__8'::'<before>5__2'
			IL_00f2: sub
			IL_00f3: box [mscorlib]System.Int32
			IL_00f8: call string [mscorlib]System.String::Format(string, object)
			IL_00fd: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0102: pop
			IL_0103: ldloc.1
			IL_0104: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0109: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_010e: stloc.2
			IL_010f: ldloc.1
			IL_0110: ldstr "a frame has two eyes and the game's controls pinned in front of you, see-through"
			IL_0115: ldloc.2
			IL_0116: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_011b: ldc.i4.2
			IL_011c: bne.un.s IL_0136

			IL_011e: ldloc.2
			IL_011f: ldfld bool FirstPerson.Vr.VrSystem/Layers::Panel
			IL_0124: brfalse.s IL_0136

			IL_0126: ldloc.2
			IL_0127: ldfld bool FirstPerson.Vr.VrSystem/Layers::PanelOnHead
			IL_012c: brtrue.s IL_0136

			IL_012e: ldloc.2
			IL_012f: ldfld bool FirstPerson.Vr.VrSystem/Layers::PanelSeeThrough
			IL_0134: br.s IL_0137

			IL_0136: ldc.i4.0

			IL_0137: ldstr "eyes {0}, panel {1} on head {2} see-through {3}"
			IL_013c: ldc.i4.4
			IL_013d: newarr [mscorlib]System.Object
			IL_0142: dup
			IL_0143: ldc.i4.0
			IL_0144: ldloc.2
			IL_0145: ldfld int32 FirstPerson.Vr.VrSystem/Layers::Eyes
			IL_014a: box [mscorlib]System.Int32
			IL_014f: stelem.ref
			IL_0150: dup
			IL_0151: ldc.i4.1
			IL_0152: ldloc.2
			IL_0153: ldfld bool FirstPerson.Vr.VrSystem/Layers::Panel
			IL_0158: box [mscorlib]System.Boolean
			IL_015d: stelem.ref
			IL_015e: dup
			IL_015f: ldc.i4.2
			IL_0160: ldloc.2
			IL_0161: ldfld bool FirstPerson.Vr.VrSystem/Layers::PanelOnHead
			IL_0166: box [mscorlib]System.Boolean
			IL_016b: stelem.ref
			IL_016c: dup
			IL_016d: ldc.i4.3
			IL_016e: ldloc.2
			IL_016f: ldfld bool FirstPerson.Vr.VrSystem/Layers::PanelSeeThrough
			IL_0174: box [mscorlib]System.Boolean
			IL_0179: stelem.ref
			IL_017a: call string [mscorlib]System.String::Format(string, object[])
			IL_017f: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0184: pop
			IL_0185: ldloc.1
			IL_0186: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_018b: ldc.i4.0
			IL_018c: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_0191: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Forward
			IL_0196: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_019b: ldc.r4 -1
			IL_01a0: ldc.r4 1
			IL_01a5: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Clamp(float32, float32, float32)
			IL_01aa: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Asin(float32)
			IL_01af: neg
			IL_01b0: ldc.r4 57.29578
			IL_01b5: mul
			IL_01b6: stloc.3
			IL_01b7: ldloc.1
			IL_01b8: ldstr "with the head level you look down on the land as the map does"
			IL_01bd: ldloc.3
			IL_01be: ldc.r4 35
			IL_01c3: ble.un.s IL_01cf

			IL_01c5: ldloc.3
			IL_01c6: ldc.r4 55
			IL_01cb: clt
			IL_01cd: br.s IL_01d0

			IL_01cf: ldc.i4.0

			IL_01d0: ldstr "{0:F1} deg down"
			IL_01d5: ldloc.3
			IL_01d6: box [mscorlib]System.Single
			IL_01db: call string [mscorlib]System.String::Format(string, object)
			IL_01e0: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_01e5: pop
			IL_01e6: ldloc.1
			IL_01e7: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_01ec: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.WorldBoxMod::get_VrGodSeatForTest()
			IL_01f1: stloc.s 4
			IL_01f3: ldloc.1
			IL_01f4: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_01f9: callvirt instance class FirstPerson.WorldSampler FirstPerson.WorldBoxMod::get_SamplerForTest()
			IL_01fe: ldfld class FirstPerson.Core.TerrainCache FirstPerson.WorldSampler::Cache
			IL_0203: ldloc.1
			IL_0204: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0209: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_020e: ldloc.1
			IL_020f: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0214: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0219: callvirt instance float32 FirstPerson.Core.TerrainCache::SampleHeight(float32, float32)
			IL_021e: stloc.s 5
			IL_0220: ldloc.1
			IL_0221: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0226: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodScaleForTest()
			IL_022b: stloc.s 6
			IL_022d: ldloc.1
			IL_022e: ldstr "you hover over the land, a few metres up at the world's scale"
			IL_0233: ldloc.s 4
			IL_0235: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_023a: ldloc.s 5
			IL_023c: sub
			IL_023d: ldc.r4 5
			IL_0242: ble.un.s IL_026e

			IL_0244: ldloc.s 4
			IL_0246: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_024b: ldloc.s 5
			IL_024d: sub
			IL_024e: ldloc.s 6
			IL_0250: div
			IL_0251: ldc.r4 2
			IL_0256: ble.un.s IL_026e

			IL_0258: ldloc.s 4
			IL_025a: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_025f: ldloc.s 5
			IL_0261: sub
			IL_0262: ldloc.s 6
			IL_0264: div
			IL_0265: ldc.r4 4.5
			IL_026a: clt
			IL_026c: br.s IL_026f

			IL_026e: ldc.i4.0

			IL_026f: ldstr "seat {0:F1} tiles up, {1:F2} tiles a metre: {2:F1} m"
			IL_0274: ldloc.s 4
			IL_0276: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_027b: ldloc.s 5
			IL_027d: sub
			IL_027e: box [mscorlib]System.Single
			IL_0283: ldloc.s 6
			IL_0285: box [mscorlib]System.Single
			IL_028a: ldloc.s 4
			IL_028c: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_0291: ldloc.s 5
			IL_0293: sub
			IL_0294: ldloc.s 6
			IL_0296: div
			IL_0297: box [mscorlib]System.Single
			IL_029c: call string [mscorlib]System.String::Format(string, object, object, object)
			IL_02a1: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_02a6: pop
			IL_02a7: ldloc.1
			IL_02a8: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02ad: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_02b2: stloc.s 7
			IL_02b4: ldloc.1
			IL_02b5: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_02ba: ldloc.s 4
			IL_02bc: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_02c1: ldloc.s 4
			IL_02c3: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_02c8: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_02cd: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_02d2: stloc.s 8
			IL_02d4: ldloc.1
			IL_02d5: ldstr "you face the middle of the view"
			IL_02da: ldloc.s 8
			IL_02dc: ldloc.s 7
			IL_02de: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
			IL_02e3: ldloc.s 7
			IL_02e5: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
			IL_02ea: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_02ef: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Angle(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_02f4: ldc.r4 5
			IL_02f9: clt
			IL_02fb: ldstr "{0:F1} deg off"
			IL_0300: ldloc.s 8
			IL_0302: ldloc.s 7
			IL_0304: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
			IL_0309: ldloc.s 7
			IL_030b: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
			IL_0310: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_0315: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Angle(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_031a: box [mscorlib]System.Single
			IL_031f: call string [mscorlib]System.String::Format(string, object)
			IL_0324: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0329: pop
			IL_032a: ldloc.1
			IL_032b: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0330: ldc.i4.0
			IL_0331: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_0336: stloc.s 9
			IL_0338: ldloc.1
			IL_0339: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_033e: ldc.i4.1
			IL_033f: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_0344: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Position
			IL_0349: ldloc.s 9
			IL_034b: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Position
			IL_0350: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0355: stloc.s 13
			IL_0357: ldloca.s 13
			IL_0359: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector3::get_magnitude()
			IL_035e: stloc.s 10
			IL_0360: ldloc.1
			IL_0361: ldstr "the eyes are an eye spacing apart at the world's scale"
			IL_0366: ldloc.s 10
			IL_0368: ldc.r4 0.064
			IL_036d: ldloc.s 6
			IL_036f: mul
			IL_0370: sub
			IL_0371: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_0376: ldc.r4 0.064
			IL_037b: ldloc.s 6
			IL_037d: mul
			IL_037e: ldc.r4 0.05
			IL_0383: mul
			IL_0384: clt
			IL_0386: ldstr "{0:F3} tiles, want {1:F3}"
			IL_038b: ldloc.s 10
			IL_038d: box [mscorlib]System.Single
			IL_0392: ldc.r4 0.064
			IL_0397: ldloc.s 6
			IL_0399: mul
			IL_039a: box [mscorlib]System.Single
			IL_039f: call string [mscorlib]System.String::Format(string, object, object)
			IL_03a4: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_03a9: pop
			IL_03aa: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_03af: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Camera ['Assembly-CSharp']MapBox::get_camera()
			IL_03b4: stloc.s 11
			IL_03b6: ldloc.1
			IL_03b7: ldstr "the map camera draws nothing (the screen holds the controls alone)"
			IL_03bc: ldloc.s 11
			IL_03be: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Camera::get_cullingMask()
			IL_03c3: brtrue.s IL_03d2

			IL_03c5: ldloc.1
			IL_03c6: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_03cb: callvirt instance bool FirstPerson.WorldBoxMod::get_GodShown()
			IL_03d0: br.s IL_03d3

			IL_03d2: ldc.i4.0

			IL_03d3: ldstr "mask 0x{0:X}"
			IL_03d8: ldloc.s 11
			IL_03da: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Camera::get_cullingMask()
			IL_03df: box [mscorlib]System.Int32
			IL_03e4: call string [mscorlib]System.String::Format(string, object)
			IL_03e9: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_03ee: pop
			IL_03ef: ldloc.1
			IL_03f0: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_03f5: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::get_VrScreenForTest()
			IL_03fa: stloc.s 12
			IL_03fc: ldloc.s 12
			IL_03fe: ldnull
			IL_03ff: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
			IL_0404: brfalse IL_04eb

			IL_0409: ldloc.s 12
			IL_040b: ldc.i4.1
			IL_040c: call class [UnityEngine.CoreModule]UnityEngine.Texture2D FirstPerson.VrUiTest::Read(class [UnityEngine.CoreModule]UnityEngine.RenderTexture, bool)
			IL_0411: stloc.s 14
			IL_0413: ldloc.s 14
			IL_0415: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color32[] [UnityEngine.CoreModule]UnityEngine.Texture2D::GetPixels32()
			IL_041a: stloc.s 15
			IL_041c: ldc.i4.0
			IL_041d: stloc.s 16
			IL_041f: ldc.i4.0
			IL_0420: stloc.s 17
			IL_0422: ldloc.s 15
			IL_0424: stloc.s 18
			IL_0426: ldc.i4.0
			IL_0427: stloc.s 19
			IL_0429: br.s IL_047e
			// loop start (head: IL_047e)
				IL_042b: ldloc.s 18
				IL_042d: ldloc.s 19
				IL_042f: ldelem [UnityEngine.CoreModule]UnityEngine.Color32
				IL_0434: stloc.s 20
				IL_0436: ldloc.s 20
				IL_0438: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::a
				IL_043d: ldc.i4 128
				IL_0442: ble.s IL_044a

				IL_0444: ldloc.s 16
				IL_0446: ldc.i4.1
				IL_0447: add
				IL_0448: stloc.s 16

				IL_044a: ldloc.s 20
				IL_044c: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::a
				IL_0451: ldc.i4.0
				IL_0452: bgt.s IL_0472

				IL_0454: ldloc.s 20
				IL_0456: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::r
				IL_045b: ldc.i4.0
				IL_045c: bgt.s IL_0472

				IL_045e: ldloc.s 20
				IL_0460: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::g
				IL_0465: ldc.i4.0
				IL_0466: bgt.s IL_0472

				IL_0468: ldloc.s 20
				IL_046a: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::b
				IL_046f: ldc.i4.0
				IL_0470: ble.s IL_0478

				IL_0472: ldloc.s 17
				IL_0474: ldc.i4.1
				IL_0475: add
				IL_0476: stloc.s 17

				IL_0478: ldloc.s 19
				IL_047a: ldc.i4.1
				IL_047b: add
				IL_047c: stloc.s 19

				IL_047e: ldloc.s 19
				IL_0480: ldloc.s 18
				IL_0482: ldlen
				IL_0483: conv.i4
				IL_0484: blt.s IL_042b
			// end loop

			IL_0486: ldloc.s 17
			IL_0488: brtrue.s IL_0497

			IL_048a: ldloc.1
			IL_048b: ldstr "the screen's capture is empty here (a minimised test copy): the desk's see-through not checked"
			IL_0490: call instance void FirstPerson.CivilTest::Note(string)
			IL_0495: br.s IL_04df

			IL_0497: ldloc.1
			IL_0498: ldstr "the desk is see-through but for the controls"
			IL_049d: ldloc.s 16
			IL_049f: ldloc.s 15
			IL_04a1: ldlen
			IL_04a2: conv.i4
			IL_04a3: ldc.i4 500
			IL_04a8: div
			IL_04a9: ble.s IL_04ba

			IL_04ab: ldloc.s 16
			IL_04ad: ldloc.s 15
			IL_04af: ldlen
			IL_04b0: conv.i4
			IL_04b1: ldc.i4.6
			IL_04b2: mul
			IL_04b3: ldc.i4.s 10
			IL_04b5: div
			IL_04b6: clt
			IL_04b8: br.s IL_04bb

			IL_04ba: ldc.i4.0

			IL_04bb: ldstr "{0:F1}% solid"
			IL_04c0: ldloc.s 16
			IL_04c2: conv.r4
			IL_04c3: ldc.r4 100
			IL_04c8: mul
			IL_04c9: ldloc.s 15
			IL_04cb: ldlen
			IL_04cc: conv.i4
			IL_04cd: conv.r4
			IL_04ce: div
			IL_04cf: box [mscorlib]System.Single
			IL_04d4: call string [mscorlib]System.String::Format(string, object)
			IL_04d9: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_04de: pop

			IL_04df: ldloc.s 14
			IL_04e1: ldstr "vrui_desk.png"
			IL_04e6: call void FirstPerson.VrUiTest::Save(class [UnityEngine.CoreModule]UnityEngine.Texture2D, string)

			IL_04eb: ldloc.1
			IL_04ec: ldstr "vrui_god_ahead"
			IL_04f1: call instance void FirstPerson.VrUiTest::SaveEyes(string)
			IL_04f6: ldc.i4.0
			IL_04f7: ret
		} // end of method '<GodEyes>d__8'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51d5a4
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>2__current'
			IL_0006: ret
		} // end of method '<GodEyes>d__8'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51d5ac
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<GodEyes>d__8'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51d5b3
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>2__current'
			IL_0006: ret
		} // end of method '<GodEyes>d__8'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<GodEyes>d__8'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<GodEyes>d__8'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <GodEyes>d__8

	.class nested private auto ansi sealed beforefieldinit '<Hold>d__20'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 'at'
		.field public float32 zoom
		.field public float32 seconds
		.field private float32 '<t>5__2'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51d5bb
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<Hold>d__20'::'<>1__state'
			IL_000d: ret
		} // end of method '<Hold>d__20'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51d5ca
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<Hold>d__20'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51d5cc
			// Header size: 12
			// Code size: 110 (0x6e)
			.maxstack 3
			.locals init (
				[0] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<Hold>d__20'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldloc.0
			IL_0008: brfalse.s IL_0010

			IL_000a: ldloc.0
			IL_000b: ldc.i4.1
			IL_000c: beq.s IL_0045

			IL_000e: ldc.i4.0
			IL_000f: ret

			IL_0010: ldarg.0
			IL_0011: ldc.i4.m1
			IL_0012: stfld int32 FirstPerson.VrUiTest/'<Hold>d__20'::'<>1__state'
			IL_0017: ldarg.0
			IL_0018: ldc.r4 0.0
			IL_001d: stfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::'<t>5__2'
			IL_0022: br.s IL_005e

			IL_0024: ldarg.0
			IL_0025: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Hold>d__20'::'at'
			IL_002a: ldarg.0
			IL_002b: ldfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::zoom
			IL_0030: call void FirstPerson.VrUiTest::HoldNow(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
			IL_0035: ldarg.0
			IL_0036: ldnull
			IL_0037: stfld object FirstPerson.VrUiTest/'<Hold>d__20'::'<>2__current'
			IL_003c: ldarg.0
			IL_003d: ldc.i4.1
			IL_003e: stfld int32 FirstPerson.VrUiTest/'<Hold>d__20'::'<>1__state'
			IL_0043: ldc.i4.1
			IL_0044: ret

			IL_0045: ldarg.0
			IL_0046: ldc.i4.m1
			IL_0047: stfld int32 FirstPerson.VrUiTest/'<Hold>d__20'::'<>1__state'
			IL_004c: ldarg.0
			IL_004d: ldarg.0
			IL_004e: ldfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::'<t>5__2'
			IL_0053: call float32 [UnityEngine.CoreModule]UnityEngine.Time::get_unscaledDeltaTime()
			IL_0058: add
			IL_0059: stfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::'<t>5__2'

			IL_005e: ldarg.0
			IL_005f: ldfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::'<t>5__2'
			IL_0064: ldarg.0
			IL_0065: ldfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::seconds
			IL_006a: blt.s IL_0024

			IL_006c: ldc.i4.0
			IL_006d: ret
		} // end of method '<Hold>d__20'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51d646
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Hold>d__20'::'<>2__current'
			IL_0006: ret
		} // end of method '<Hold>d__20'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51d64e
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<Hold>d__20'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51d655
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Hold>d__20'::'<>2__current'
			IL_0006: ret
		} // end of method '<Hold>d__20'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<Hold>d__20'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<Hold>d__20'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <Hold>d__20

	.class nested private auto ansi sealed beforefieldinit '<LaserButton>d__14'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private string '<want>5__2'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<pixel>5__3'
		.field private valuetype FirstPerson.Vr.Xr/Pose '<aim>5__4'
		.field private bool '<held>5__5'
		.field private string '<trace>5__6'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<drift>5__7'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<first>5__8'
		.field private int32 '<i>5__9'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51d65d
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_000d: ret
		} // end of method '<LaserButton>d__14'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51d66c
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<LaserButton>d__14'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51d670
			// Header size: 12
			// Code size: 2660 (0xa64)
			.maxstack 8
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] class ['Assembly-CSharp']PowerButton,
				[3] valuetype [UnityEngine.CoreModule]UnityEngine.Rect,
				[4] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[5] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']PowerButton>,
				[6] class ['Assembly-CSharp']PowerButton,
				[7] class [UnityEngine.CoreModule]UnityEngine.RectTransform,
				[8] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3[],
				[9] valuetype [UnityEngine.CoreModule]UnityEngine.Rect,
				[10] string,
				[11] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[12] float32,
				[13] valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion,
				[14] valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion,
				[15] valuetype FirstPerson.Vr.Xr/Pose,
				[16] valuetype FirstPerson.Vr.Xr/Quat,
				[17] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_004e, IL_0276, IL_034a, IL_0386, IL_042f, IL_048d, IL_04c9, IL_0505, IL_0599, IL_05f8, IL_0674, IL_07e5, IL_0963, IL_0a46)

			IL_004c: ldc.i4.0
			IL_004d: ret

			IL_004e: ldarg.0
			IL_004f: ldc.i4.m1
			IL_0050: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0055: ldnull
			IL_0056: stloc.2
			IL_0057: ldloca.s 3
			IL_0059: initobj [UnityEngine.CoreModule]UnityEngine.Rect
			IL_005f: ldsfld class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']PowerButton> ['Assembly-CSharp']PowerButton::power_buttons
			IL_0064: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']PowerButton>::GetEnumerator()
			IL_0069: stloc.s 5
			.try
			{
				IL_006b: br IL_01b0
				// loop start (head: IL_01b0)
					IL_0070: ldloca.s 5
					IL_0072: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']PowerButton>::get_Current()
					IL_0077: stloc.s 6
					IL_0079: ldloc.s 6
					IL_007b: ldnull
					IL_007c: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
					IL_0081: brtrue IL_01b0

					IL_0086: ldloc.s 6
					IL_0088: callvirt instance class [UnityEngine.CoreModule]UnityEngine.GameObject [UnityEngine.CoreModule]UnityEngine.Component::get_gameObject()
					IL_008d: callvirt instance bool [UnityEngine.CoreModule]UnityEngine.GameObject::get_activeInHierarchy()
					IL_0092: brfalse IL_01b0

					IL_0097: ldloc.s 6
					IL_0099: ldfld class ['Assembly-CSharp']GodPower ['Assembly-CSharp']PowerButton::godPower
					IL_009e: brfalse IL_01b0

					IL_00a3: ldloc.s 6
					IL_00a5: ldfld valuetype ['Assembly-CSharp']PowerButtonType ['Assembly-CSharp']PowerButton::'type'
					IL_00aa: ldc.i4.2
					IL_00ab: bne.un IL_01b0

					IL_00b0: ldloc.s 6
					IL_00b2: ldfld string ['Assembly-CSharp']PowerButton::open_window_id
					IL_00b7: call bool [mscorlib]System.String::IsNullOrEmpty(string)
					IL_00bc: brfalse IL_01b0

					IL_00c1: ldloc.s 6
					IL_00c3: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
					IL_00c8: isinst [UnityEngine.CoreModule]UnityEngine.RectTransform
					IL_00cd: stloc.s 7
					IL_00cf: ldloc.s 7
					IL_00d1: ldnull
					IL_00d2: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
					IL_00d7: brtrue IL_01b0

					IL_00dc: ldc.i4.4
					IL_00dd: newarr [UnityEngine.CoreModule]UnityEngine.Vector3
					IL_00e2: stloc.s 8
					IL_00e4: ldloc.s 7
					IL_00e6: ldloc.s 8
					IL_00e8: callvirt instance void [UnityEngine.CoreModule]UnityEngine.RectTransform::GetWorldCorners(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3[])
					IL_00ed: ldloc.s 8
					IL_00ef: ldc.i4.0
					IL_00f0: ldelema [UnityEngine.CoreModule]UnityEngine.Vector3
					IL_00f5: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
					IL_00fa: ldloc.s 8
					IL_00fc: ldc.i4.0
					IL_00fd: ldelema [UnityEngine.CoreModule]UnityEngine.Vector3
					IL_0102: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
					IL_0107: ldloc.s 8
					IL_0109: ldc.i4.2
					IL_010a: ldelema [UnityEngine.CoreModule]UnityEngine.Vector3
					IL_010f: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
					IL_0114: ldloc.s 8
					IL_0116: ldc.i4.2
					IL_0117: ldelema [UnityEngine.CoreModule]UnityEngine.Vector3
					IL_011c: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
					IL_0121: call valuetype [UnityEngine.CoreModule]UnityEngine.Rect [UnityEngine.CoreModule]UnityEngine.Rect::MinMaxRect(float32, float32, float32, float32)
					IL_0126: stloc.s 9
					IL_0128: ldloca.s 9
					IL_012a: call instance float32 [UnityEngine.CoreModule]UnityEngine.Rect::get_width()
					IL_012f: ldc.r4 8
					IL_0134: blt.s IL_01b0

					IL_0136: ldloca.s 9
					IL_0138: call instance float32 [UnityEngine.CoreModule]UnityEngine.Rect::get_xMin()
					IL_013d: ldc.r4 0.0
					IL_0142: blt.s IL_01b0

					IL_0144: ldloca.s 9
					IL_0146: call instance float32 [UnityEngine.CoreModule]UnityEngine.Rect::get_yMin()
					IL_014b: ldc.r4 0.0
					IL_0150: blt.s IL_01b0

					IL_0152: ldloca.s 9
					IL_0154: call instance float32 [UnityEngine.CoreModule]UnityEngine.Rect::get_xMax()
					IL_0159: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_width()
					IL_015e: conv.r4
					IL_015f: bgt.s IL_01b0

					IL_0161: ldloca.s 9
					IL_0163: call instance float32 [UnityEngine.CoreModule]UnityEngine.Rect::get_yMax()
					IL_0168: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_height()
					IL_016d: conv.r4
					IL_016e: bgt.s IL_01b0

					IL_0170: ldloc.s 6
					IL_0172: ldfld class ['Assembly-CSharp']GodPower ['Assembly-CSharp']PowerButton::godPower
					IL_0177: ldfld string ['Assembly-CSharp']Asset::id
					IL_017c: stloc.s 10
					IL_017e: ldloc.s 10
					IL_0180: ldstr "sheep"
					IL_0185: call bool [mscorlib]System.String::op_Equality(string, string)
					IL_018a: brtrue.s IL_01b0

					IL_018c: ldloc.s 10
					IL_018e: ldstr "pause"
					IL_0193: callvirt instance bool [mscorlib]System.String::Contains(string)
					IL_0198: brtrue.s IL_01b0

					IL_019a: ldloc.s 10
					IL_019c: ldstr "clock"
					IL_01a1: callvirt instance bool [mscorlib]System.String::Contains(string)
					IL_01a6: brtrue.s IL_01b0

					IL_01a8: ldloc.s 6
					IL_01aa: stloc.2
					IL_01ab: ldloc.s 9
					IL_01ad: stloc.3
					IL_01ae: leave.s IL_01cc

					IL_01b0: ldloca.s 5
					IL_01b2: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']PowerButton>::MoveNext()
					IL_01b7: brtrue IL_0070
				// end loop

				IL_01bc: leave.s IL_01cc
			} // end .try
			finally
			{
				IL_01be: ldloca.s 5
				IL_01c0: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']PowerButton>
				IL_01c6: callvirt instance void [mscorlib]System.IDisposable::Dispose()
				IL_01cb: endfinally
			} // end handler

			IL_01cc: ldloc.1
			IL_01cd: ldstr "a power button of the game's shows on the screen"
			IL_01d2: ldloc.2
			IL_01d3: ldnull
			IL_01d4: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
			IL_01d9: ldnull
			IL_01da: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_01df: brtrue.s IL_01e3

			IL_01e1: ldc.i4.0
			IL_01e2: ret

			IL_01e3: ldarg.0
			IL_01e4: ldloc.2
			IL_01e5: ldfld class ['Assembly-CSharp']GodPower ['Assembly-CSharp']PowerButton::godPower
			IL_01ea: ldfld string ['Assembly-CSharp']Asset::id
			IL_01ef: stfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<want>5__2'
			.try
			{
				IL_01f4: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
				IL_01f9: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::unselectAll()
				IL_01fe: leave.s IL_0203
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_0200: pop
				IL_0201: leave.s IL_0203
			} // end handler

			IL_0203: ldarg.0
			IL_0204: ldloca.s 3
			IL_0206: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Rect::get_center()
			IL_020b: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_0210: ldloc.1
			IL_0211: ldstr "the controls on your view take the laser"
			IL_0216: ldloc.1
			IL_0217: ldarg.0
			IL_0218: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_021d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0222: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_width()
			IL_0227: conv.r4
			IL_0228: div
			IL_0229: ldc.r4 1
			IL_022e: ldarg.0
			IL_022f: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_0234: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0239: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_height()
			IL_023e: conv.r4
			IL_023f: div
			IL_0240: sub
			IL_0241: ldc.i4.1
			IL_0242: call instance bool FirstPerson.VrUiTest::PointAtPanel(float32, float32, int32)
			IL_0247: ldnull
			IL_0248: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_024d: brtrue.s IL_0251

			IL_024f: ldc.i4.0
			IL_0250: ret

			IL_0251: ldarg.0
			IL_0252: ldloc.1
			IL_0253: ldloc.1
			IL_0254: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0259: ldc.r4 20
			IL_025e: ldc.r4 0.4
			IL_0263: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0268: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_026d: ldarg.0
			IL_026e: ldc.i4.1
			IL_026f: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0274: ldc.i4.1
			IL_0275: ret

			IL_0276: ldarg.0
			IL_0277: ldc.i4.m1
			IL_0278: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_027d: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_0282: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0287: stloc.s 4
			IL_0289: ldloc.1
			IL_028a: ldstr "over the game's button, the mouse is on it"
			IL_028f: call valuetype FirstPerson.Vr.VrMouse/Laser FirstPerson.Vr.VrMouse::get_Now()
			IL_0294: ldfld bool FirstPerson.Vr.VrMouse/Laser::OnScreen
			IL_0299: brfalse.s IL_02ba

			IL_029b: ldloc.s 4
			IL_029d: ldarg.0
			IL_029e: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_02a3: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_02a8: stloc.s 11
			IL_02aa: ldloca.s 11
			IL_02ac: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector2::get_magnitude()
			IL_02b1: ldc.r4 3
			IL_02b6: clt
			IL_02b8: br.s IL_02bb

			IL_02ba: ldc.i4.0

			IL_02bb: ldstr "mouse {0}, button {1}"
			IL_02c0: ldloc.s 4
			IL_02c2: box [UnityEngine.CoreModule]UnityEngine.Vector2
			IL_02c7: ldarg.0
			IL_02c8: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_02cd: box [UnityEngine.CoreModule]UnityEngine.Vector2
			IL_02d2: call string [mscorlib]System.String::Format(string, object, object)
			IL_02d7: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_02dc: pop
			IL_02dd: ldloc.1
			IL_02de: ldstr "the beam reaches the controls"
			IL_02e3: ldloc.1
			IL_02e4: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02e9: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrBeamMetresForTest()
			IL_02ee: ldc.r4 0.5
			IL_02f3: cgt
			IL_02f5: ldstr "{0:F2} m"
			IL_02fa: ldloc.1
			IL_02fb: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0300: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrBeamMetresForTest()
			IL_0305: box [mscorlib]System.Single
			IL_030a: call string [mscorlib]System.String::Format(string, object)
			IL_030f: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0314: pop
			IL_0315: ldloc.1
			IL_0316: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_031b: ldc.r4 1
			IL_0320: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_0325: ldarg.0
			IL_0326: ldloc.1
			IL_0327: ldloc.1
			IL_0328: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_032d: ldc.r4 20
			IL_0332: ldc.r4 0.15
			IL_0337: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_033c: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0341: ldarg.0
			IL_0342: ldc.i4.2
			IL_0343: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0348: ldc.i4.1
			IL_0349: ret

			IL_034a: ldarg.0
			IL_034b: ldc.i4.m1
			IL_034c: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0351: ldloc.1
			IL_0352: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0357: ldc.r4 0.0
			IL_035c: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_0361: ldarg.0
			IL_0362: ldloc.1
			IL_0363: ldloc.1
			IL_0364: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0369: ldc.r4 20
			IL_036e: ldc.r4 0.4
			IL_0373: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0378: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_037d: ldarg.0
			IL_037e: ldc.i4.3
			IL_037f: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0384: ldc.i4.1
			IL_0385: ret

			IL_0386: ldarg.0
			IL_0387: ldc.i4.m1
			IL_0388: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_038d: ldloc.1
			IL_038e: ldstr "the trigger pressed the game's button (its power picked)"
			IL_0393: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0398: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_039d: ldarg.0
			IL_039e: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<want>5__2'
			IL_03a3: call bool [mscorlib]System.String::op_Equality(string, string)
			IL_03a8: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_03ad: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_03b2: ldstr " (want "
			IL_03b7: ldarg.0
			IL_03b8: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<want>5__2'
			IL_03bd: ldstr ")"
			IL_03c2: call string [mscorlib]System.String::Concat(string, string, string, string)
			IL_03c7: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_03cc: pop
			IL_03cd: ldloc.1
			IL_03ce: ldc.r4 0.0
			IL_03d3: ldc.r4 1
			IL_03d8: ldc.r4 0.0
			IL_03dd: ldc.r4 30
			IL_03e2: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_03e7: ldc.r4 0.0
			IL_03ec: ldc.r4 0.0
			IL_03f1: ldc.r4 0.0
			IL_03f6: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			.try
			{
				IL_03fb: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
				IL_0400: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::unselectAll()
				IL_0405: leave.s IL_040a
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_0407: pop
				IL_0408: leave.s IL_040a
			} // end handler

			IL_040a: ldarg.0
			IL_040b: ldloc.1
			IL_040c: ldloc.1
			IL_040d: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0412: ldc.r4 20
			IL_0417: ldc.r4 0.3
			IL_041c: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0421: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0426: ldarg.0
			IL_0427: ldc.i4.4
			IL_0428: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_042d: ldc.i4.1
			IL_042e: ret

			IL_042f: ldarg.0
			IL_0430: ldc.i4.m1
			IL_0431: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0436: ldloc.1
			IL_0437: ldarg.0
			IL_0438: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_043d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0442: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_width()
			IL_0447: conv.r4
			IL_0448: div
			IL_0449: ldc.r4 1
			IL_044e: ldarg.0
			IL_044f: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_0454: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0459: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_height()
			IL_045e: conv.r4
			IL_045f: div
			IL_0460: sub
			IL_0461: ldc.i4.1
			IL_0462: call instance bool FirstPerson.VrUiTest::PointAtPanel(float32, float32, int32)
			IL_0467: pop
			IL_0468: ldarg.0
			IL_0469: ldloc.1
			IL_046a: ldloc.1
			IL_046b: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0470: ldc.r4 20
			IL_0475: ldc.r4 0.3
			IL_047a: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_047f: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0484: ldarg.0
			IL_0485: ldc.i4.5
			IL_0486: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_048b: ldc.i4.1
			IL_048c: ret

			IL_048d: ldarg.0
			IL_048e: ldc.i4.m1
			IL_048f: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0494: ldloc.1
			IL_0495: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_049a: ldc.r4 1
			IL_049f: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_04a4: ldarg.0
			IL_04a5: ldloc.1
			IL_04a6: ldloc.1
			IL_04a7: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_04ac: ldc.r4 20
			IL_04b1: ldc.r4 0.15
			IL_04b6: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_04bb: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_04c0: ldarg.0
			IL_04c1: ldc.i4.6
			IL_04c2: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_04c7: ldc.i4.1
			IL_04c8: ret

			IL_04c9: ldarg.0
			IL_04ca: ldc.i4.m1
			IL_04cb: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_04d0: ldloc.1
			IL_04d1: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_04d6: ldc.r4 0.0
			IL_04db: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_04e0: ldarg.0
			IL_04e1: ldloc.1
			IL_04e2: ldloc.1
			IL_04e3: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_04e8: ldc.r4 20
			IL_04ed: ldc.r4 0.4
			IL_04f2: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_04f7: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_04fc: ldarg.0
			IL_04fd: ldc.i4.7
			IL_04fe: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0503: ldc.i4.1
			IL_0504: ret

			IL_0505: ldarg.0
			IL_0506: ldc.i4.m1
			IL_0507: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_050c: ldloc.1
			IL_050d: ldstr "with the head turned the controls stay put and still press"
			IL_0512: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0517: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_051c: ldarg.0
			IL_051d: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<want>5__2'
			IL_0522: call bool [mscorlib]System.String::op_Equality(string, string)
			IL_0527: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_052c: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_0531: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0536: pop
			IL_0537: ldloc.1
			IL_0538: ldc.r4 0.0
			IL_053d: ldc.r4 1
			IL_0542: ldc.r4 0.0
			IL_0547: ldc.r4 0.0
			IL_054c: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_0551: ldc.r4 0.0
			IL_0556: ldc.r4 0.0
			IL_055b: ldc.r4 0.0
			IL_0560: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			.try
			{
				IL_0565: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
				IL_056a: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::unselectAll()
				IL_056f: leave.s IL_0574
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_0571: pop
				IL_0572: leave.s IL_0574
			} // end handler

			IL_0574: ldarg.0
			IL_0575: ldloc.1
			IL_0576: ldloc.1
			IL_0577: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_057c: ldc.r4 20
			IL_0581: ldc.r4 0.8
			IL_0586: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_058b: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0590: ldarg.0
			IL_0591: ldc.i4.8
			IL_0592: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0597: ldc.i4.1
			IL_0598: ret

			IL_0599: ldarg.0
			IL_059a: ldc.i4.m1
			IL_059b: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_05a0: ldloc.1
			IL_05a1: ldarg.0
			IL_05a2: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_05a7: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_05ac: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_width()
			IL_05b1: conv.r4
			IL_05b2: div
			IL_05b3: ldc.r4 1
			IL_05b8: ldarg.0
			IL_05b9: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<pixel>5__3'
			IL_05be: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_05c3: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_height()
			IL_05c8: conv.r4
			IL_05c9: div
			IL_05ca: sub
			IL_05cb: ldc.i4.1
			IL_05cc: call instance bool FirstPerson.VrUiTest::PointAtPanel(float32, float32, int32)
			IL_05d1: pop
			IL_05d2: ldarg.0
			IL_05d3: ldloc.1
			IL_05d4: ldloc.1
			IL_05d5: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_05da: ldc.r4 20
			IL_05df: ldc.r4 0.4
			IL_05e4: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_05e9: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_05ee: ldarg.0
			IL_05ef: ldc.i4.s 9
			IL_05f1: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_05f6: ldc.i4.1
			IL_05f7: ret

			IL_05f8: ldarg.0
			IL_05f9: ldc.i4.m1
			IL_05fa: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_05ff: ldarg.0
			IL_0600: ldloc.1
			IL_0601: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0606: ldfld valuetype FirstPerson.Vr.Xr/Pose[] FirstPerson.Vr.VrSystem::SimHands
			IL_060b: ldc.i4.1
			IL_060c: ldelem FirstPerson.Vr.Xr/Pose
			IL_0611: stfld valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_0616: ldarg.0
			IL_0617: ldc.i4.0
			IL_0618: stfld bool FirstPerson.VrUiTest/'<LaserButton>d__14'::'<held>5__5'
			IL_061d: ldarg.0
			IL_061e: ldstr ""
			IL_0623: stfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<trace>5__6'
			IL_0628: ldarg.0
			IL_0629: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_zero()
			IL_062e: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<drift>5__7'
			IL_0633: ldarg.0
			IL_0634: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_0639: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_063e: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<first>5__8'
			IL_0643: ldloc.1
			IL_0644: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0649: ldc.r4 0.15
			IL_064e: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_0653: ldloc.1
			IL_0654: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0659: ldc.r4 20
			IL_065e: call void FirstPerson.VrUiTest::HoldNow(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
			IL_0663: ldarg.0
			IL_0664: ldnull
			IL_0665: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_066a: ldarg.0
			IL_066b: ldc.i4.s 10
			IL_066d: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0672: ldc.i4.1
			IL_0673: ret

			IL_0674: ldarg.0
			IL_0675: ldc.i4.m1
			IL_0676: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_067b: ldarg.0
			IL_067c: ldc.i4.0
			IL_067d: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_0682: br IL_08f3

			IL_0687: ldloc.1
			IL_0688: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_068d: ldarg.0
			IL_068e: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_0693: ldc.i4.s 10
			IL_0695: blt.s IL_069e

			IL_0697: ldc.r4 0.0
			IL_069c: br.s IL_06b2

			IL_069e: ldarg.0
			IL_069f: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_06a4: ldc.i4.1
			IL_06a5: add
			IL_06a6: conv.r4
			IL_06a7: ldc.r4 6
			IL_06ac: div
			IL_06ad: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Clamp01(float32)

			IL_06b2: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_06b7: ldarg.0
			IL_06b8: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_06bd: ldc.i4.2
			IL_06be: rem
			IL_06bf: brfalse.s IL_06c8

			IL_06c1: ldc.r4 -1.2
			IL_06c6: br.s IL_06cd

			IL_06c8: ldc.r4 1.2

			IL_06cd: ldarg.0
			IL_06ce: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_06d3: ldc.i4.6
			IL_06d4: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::Min(int32, int32)
			IL_06d9: neg
			IL_06da: conv.r4
			IL_06db: ldc.r4 0.25
			IL_06e0: mul
			IL_06e1: stloc.s 12
			IL_06e3: ldloca.s 13
			IL_06e5: ldarg.0
			IL_06e6: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_06eb: ldflda valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
			IL_06f0: ldfld float32 FirstPerson.Vr.Xr/Quat::X
			IL_06f5: ldarg.0
			IL_06f6: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_06fb: ldflda valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
			IL_0700: ldfld float32 FirstPerson.Vr.Xr/Quat::Y
			IL_0705: ldarg.0
			IL_0706: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_070b: ldflda valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
			IL_0710: ldfld float32 FirstPerson.Vr.Xr/Quat::Z
			IL_0715: ldarg.0
			IL_0716: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_071b: ldflda valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
			IL_0720: ldfld float32 FirstPerson.Vr.Xr/Quat::W
			IL_0725: call instance void [UnityEngine.CoreModule]UnityEngine.Quaternion::.ctor(float32, float32, float32, float32)
			IL_072a: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_up()
			IL_072f: call valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Quaternion::AngleAxis(float32, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0734: ldloc.s 12
			IL_0736: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_right()
			IL_073b: call valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Quaternion::AngleAxis(float32, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0740: call valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Quaternion::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion, valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion)
			IL_0745: ldloc.s 13
			IL_0747: call valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Quaternion::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion, valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion)
			IL_074c: stloc.s 14
			IL_074e: ldloc.1
			IL_074f: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0754: ldfld valuetype FirstPerson.Vr.Xr/Pose[] FirstPerson.Vr.VrSystem::SimHands
			IL_0759: ldc.i4.1
			IL_075a: ldloca.s 15
			IL_075c: initobj FirstPerson.Vr.Xr/Pose
			IL_0762: ldloca.s 15
			IL_0764: ldloca.s 16
			IL_0766: initobj FirstPerson.Vr.Xr/Quat
			IL_076c: ldloca.s 16
			IL_076e: ldloc.s 14
			IL_0770: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::x
			IL_0775: stfld float32 FirstPerson.Vr.Xr/Quat::X
			IL_077a: ldloca.s 16
			IL_077c: ldloc.s 14
			IL_077e: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::y
			IL_0783: stfld float32 FirstPerson.Vr.Xr/Quat::Y
			IL_0788: ldloca.s 16
			IL_078a: ldloc.s 14
			IL_078c: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::z
			IL_0791: stfld float32 FirstPerson.Vr.Xr/Quat::Z
			IL_0796: ldloca.s 16
			IL_0798: ldloc.s 14
			IL_079a: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::w
			IL_079f: stfld float32 FirstPerson.Vr.Xr/Quat::W
			IL_07a4: ldloc.s 16
			IL_07a6: stfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
			IL_07ab: ldloca.s 15
			IL_07ad: ldarg.0
			IL_07ae: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_07b3: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_07b8: stfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_07bd: ldloc.s 15
			IL_07bf: stelem FirstPerson.Vr.Xr/Pose
			IL_07c4: ldloc.1
			IL_07c5: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_07ca: ldc.r4 20
			IL_07cf: call void FirstPerson.VrUiTest::HoldNow(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
			IL_07d4: ldarg.0
			IL_07d5: ldnull
			IL_07d6: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_07db: ldarg.0
			IL_07dc: ldc.i4.s 11
			IL_07de: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_07e3: ldc.i4.1
			IL_07e4: ret

			IL_07e5: ldarg.0
			IL_07e6: ldc.i4.m1
			IL_07e7: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_07ec: ldarg.0
			IL_07ed: ldarg.0
			IL_07ee: ldfld bool FirstPerson.VrUiTest/'<LaserButton>d__14'::'<held>5__5'
			IL_07f3: ldloc.1
			IL_07f4: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_07f9: callvirt instance bool FirstPerson.WorldBoxMod::get_VrRayHeldForTest()
			IL_07fe: or
			IL_07ff: stfld bool FirstPerson.VrUiTest/'<LaserButton>d__14'::'<held>5__5'
			IL_0804: ldarg.0
			IL_0805: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_080a: ldc.i4.s 10
			IL_080c: bge.s IL_085a

			IL_080e: ldarg.0
			IL_080f: ldarg.0
			IL_0810: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<drift>5__7'
			IL_0815: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_081a: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_081f: ldarg.0
			IL_0820: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<first>5__8'
			IL_0825: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_082a: sub
			IL_082b: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_0830: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_0835: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_083a: ldarg.0
			IL_083b: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<first>5__8'
			IL_0840: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0845: sub
			IL_0846: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_084b: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_0850: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::Max(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0855: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<drift>5__7'

			IL_085a: ldarg.0
			IL_085b: ldarg.0
			IL_085c: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<trace>5__6'
			IL_0861: ldstr " {0}:{1:F0},{2:F0}{3}"
			IL_0866: ldc.i4.4
			IL_0867: newarr [mscorlib]System.Object
			IL_086c: dup
			IL_086d: ldc.i4.0
			IL_086e: ldarg.0
			IL_086f: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_0874: box [mscorlib]System.Int32
			IL_0879: stelem.ref
			IL_087a: dup
			IL_087b: ldc.i4.1
			IL_087c: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_0881: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_0886: ldarg.0
			IL_0887: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<first>5__8'
			IL_088c: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0891: sub
			IL_0892: box [mscorlib]System.Single
			IL_0897: stelem.ref
			IL_0898: dup
			IL_0899: ldc.i4.2
			IL_089a: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_089f: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_08a4: ldarg.0
			IL_08a5: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<first>5__8'
			IL_08aa: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_08af: sub
			IL_08b0: box [mscorlib]System.Single
			IL_08b5: stelem.ref
			IL_08b6: dup
			IL_08b7: ldc.i4.3
			IL_08b8: ldloc.1
			IL_08b9: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_08be: callvirt instance bool FirstPerson.WorldBoxMod::get_VrRayHeldForTest()
			IL_08c3: brtrue.s IL_08cc

			IL_08c5: ldstr ""
			IL_08ca: br.s IL_08d1

			IL_08cc: ldstr "h"

			IL_08d1: stelem.ref
			IL_08d2: call string [mscorlib]System.String::Format(string, object[])
			IL_08d7: call string [mscorlib]System.String::Concat(string, string)
			IL_08dc: stfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<trace>5__6'
			IL_08e1: ldarg.0
			IL_08e2: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_08e7: stloc.s 17
			IL_08e9: ldarg.0
			IL_08ea: ldloc.s 17
			IL_08ec: ldc.i4.1
			IL_08ed: add
			IL_08ee: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'

			IL_08f3: ldarg.0
			IL_08f4: ldfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<i>5__9'
			IL_08f9: ldc.i4.s 16
			IL_08fb: blt IL_0687

			IL_0900: ldloc.1
			IL_0901: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0906: ldfld valuetype FirstPerson.Vr.Xr/Pose[] FirstPerson.Vr.VrSystem::SimHands
			IL_090b: ldc.i4.1
			IL_090c: ldarg.0
			IL_090d: ldfld valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<LaserButton>d__14'::'<aim>5__4'
			IL_0912: stelem FirstPerson.Vr.Xr/Pose
			IL_0917: ldloc.1
			IL_0918: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_091d: ldc.r4 0.0
			IL_0922: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_0927: ldloc.1
			IL_0928: ldstr "shaky press, the mouse each frame:"
			IL_092d: ldarg.0
			IL_092e: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<trace>5__6'
			IL_0933: call string [mscorlib]System.String::Concat(string, string)
			IL_0938: call instance void FirstPerson.CivilTest::Note(string)
			IL_093d: ldarg.0
			IL_093e: ldloc.1
			IL_093f: ldloc.1
			IL_0940: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0945: ldc.r4 20
			IL_094a: ldc.r4 0.4
			IL_094f: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0954: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0959: ldarg.0
			IL_095a: ldc.i4.s 12
			IL_095c: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0961: ldc.i4.1
			IL_0962: ret

			IL_0963: ldarg.0
			IL_0964: ldc.i4.m1
			IL_0965: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_096a: ldloc.1
			IL_096b: ldstr "a shaky press still presses the game's button (the point held still through it)"
			IL_0970: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0975: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_097a: ldarg.0
			IL_097b: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<want>5__2'
			IL_0980: call bool [mscorlib]System.String::op_Equality(string, string)
			IL_0985: ldarg.0
			IL_0986: ldfld bool FirstPerson.VrUiTest/'<LaserButton>d__14'::'<held>5__5'
			IL_098b: and
			IL_098c: brfalse.s IL_09a2

			IL_098e: ldarg.0
			IL_098f: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<drift>5__7'
			IL_0994: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector2::get_magnitude()
			IL_0999: ldc.r4 4
			IL_099e: clt
			IL_09a0: br.s IL_09a3

			IL_09a2: ldc.i4.0

			IL_09a3: ldstr "{0} (want {1}), held {2}, slid {3:F0},{4:F0} px"
			IL_09a8: ldc.i4.5
			IL_09a9: newarr [mscorlib]System.Object
			IL_09ae: dup
			IL_09af: ldc.i4.0
			IL_09b0: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_09b5: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_09ba: stelem.ref
			IL_09bb: dup
			IL_09bc: ldc.i4.1
			IL_09bd: ldarg.0
			IL_09be: ldfld string FirstPerson.VrUiTest/'<LaserButton>d__14'::'<want>5__2'
			IL_09c3: stelem.ref
			IL_09c4: dup
			IL_09c5: ldc.i4.2
			IL_09c6: ldarg.0
			IL_09c7: ldfld bool FirstPerson.VrUiTest/'<LaserButton>d__14'::'<held>5__5'
			IL_09cc: box [mscorlib]System.Boolean
			IL_09d1: stelem.ref
			IL_09d2: dup
			IL_09d3: ldc.i4.3
			IL_09d4: ldarg.0
			IL_09d5: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<drift>5__7'
			IL_09da: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_09df: box [mscorlib]System.Single
			IL_09e4: stelem.ref
			IL_09e5: dup
			IL_09e6: ldc.i4.4
			IL_09e7: ldarg.0
			IL_09e8: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<drift>5__7'
			IL_09ed: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_09f2: box [mscorlib]System.Single
			IL_09f7: stelem.ref
			IL_09f8: call string [mscorlib]System.String::Format(string, object[])
			IL_09fd: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0a02: pop
			.try
			{
				IL_0a03: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
				IL_0a08: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::unselectAll()
				IL_0a0d: leave.s IL_0a12
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_0a0f: pop
				IL_0a10: leave.s IL_0a12
			} // end handler

			IL_0a12: ldloc.1
			IL_0a13: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0a18: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
			IL_0a1d: ldc.i4.1
			IL_0a1e: ldc.i4.0
			IL_0a1f: stelem.i1
			IL_0a20: ldarg.0
			IL_0a21: ldloc.1
			IL_0a22: ldloc.1
			IL_0a23: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0a28: ldc.r4 20
			IL_0a2d: ldc.r4 0.3
			IL_0a32: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0a37: stfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0a3c: ldarg.0
			IL_0a3d: ldc.i4.s 13
			IL_0a3f: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0a44: ldc.i4.1
			IL_0a45: ret

			IL_0a46: ldarg.0
			IL_0a47: ldc.i4.m1
			IL_0a48: stfld int32 FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>1__state'
			IL_0a4d: ldloc.1
			IL_0a4e: ldstr "with no controller tracked the laser lets the mouse go"
			IL_0a53: call bool FirstPerson.Vr.VrMouse::get_Driving()
			IL_0a58: ldc.i4.0
			IL_0a59: ceq
			IL_0a5b: ldnull
			IL_0a5c: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0a61: pop
			IL_0a62: ldc.i4.0
			IL_0a63: ret
		} // end of method '<LaserButton>d__14'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51e15c
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0006: ret
		} // end of method '<LaserButton>d__14'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51e164
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<LaserButton>d__14'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51e16b
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>2__current'
			IL_0006: ret
		} // end of method '<LaserButton>d__14'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<LaserButton>d__14'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<LaserButton>d__14'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <LaserButton>d__14

	.class nested private auto ansi sealed beforefieldinit '<LaserLand>d__12'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<was>5__2'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51e173
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_000d: ret
		} // end of method '<LaserLand>d__12'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51e182
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<LaserLand>d__12'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51e184
			// Header size: 12
			// Code size: 894 (0x37e)
			.maxstack 7
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] float32,
				[3] float32,
				[4] float32,
				[5] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[6] bool,
				[7] valuetype FirstPerson.Vr.Xr/Vec3
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_0026, IL_00ac, IL_01fc, IL_02df)

			IL_0024: ldc.i4.0
			IL_0025: ret

			IL_0026: ldarg.0
			IL_0027: ldc.i4.m1
			IL_0028: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_002d: ldloc.1
			IL_002e: ldstr "Input's mouse answers for the laser"
			IL_0033: call bool FirstPerson.Vr.VrMouse::get_Patched()
			IL_0038: ldsfld string FirstPerson.Vr.VrMouse::Error
			IL_003d: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0042: pop
			IL_0043: ldloc.1
			IL_0044: ldloca.s 7
			IL_0046: initobj FirstPerson.Vr.Xr/Vec3
			IL_004c: ldloca.s 7
			IL_004e: ldc.r4 0.0
			IL_0053: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_0058: ldloca.s 7
			IL_005a: ldc.r4 0.0
			IL_005f: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_0064: ldloca.s 7
			IL_0066: ldc.r4 -1
			IL_006b: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_0070: ldloc.s 7
			IL_0072: ldc.r4 0.15
			IL_0077: ldc.r4 -0.25
			IL_007c: ldc.r4 -0.2
			IL_0081: ldc.i4.1
			IL_0082: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_0087: ldarg.0
			IL_0088: ldloc.1
			IL_0089: ldloc.1
			IL_008a: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_008f: ldc.r4 20
			IL_0094: ldc.r4 0.6
			IL_0099: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_009e: stfld object FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>2__current'
			IL_00a3: ldarg.0
			IL_00a4: ldc.i4.1
			IL_00a5: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_00aa: ldc.i4.1
			IL_00ab: ret

			IL_00ac: ldarg.0
			IL_00ad: ldc.i4.m1
			IL_00ae: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_00b3: call void FirstPerson.GodView::Resolve()
			IL_00b8: ldloc.1
			IL_00b9: ldstr "the laser points into the land"
			IL_00be: ldsfld bool FirstPerson.GodView::LaserOn
			IL_00c3: brfalse.s IL_00cc

			IL_00c5: ldsfld bool FirstPerson.GodView::MouseValid
			IL_00ca: br.s IL_00cd

			IL_00cc: ldc.i4.0

			IL_00cd: ldstr "laser {0}, on the land {1}"
			IL_00d2: ldsfld bool FirstPerson.GodView::LaserOn
			IL_00d7: box [mscorlib]System.Boolean
			IL_00dc: ldsfld bool FirstPerson.GodView::MouseValid
			IL_00e1: box [mscorlib]System.Boolean
			IL_00e6: call string [mscorlib]System.String::Format(string, object, object)
			IL_00eb: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_00f0: pop
			IL_00f1: ldloc.1
			IL_00f2: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_00f7: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodScaleForTest()
			IL_00fc: stloc.2
			IL_00fd: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.GodView::LandPoint
			IL_0102: ldloc.1
			IL_0103: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0108: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_010d: stloc.3
			IL_010e: ldloc.1
			IL_010f: ldstr "it meets the land about the middle of the view (the hand's offset at the world's scale)"
			IL_0114: ldloc.3
			IL_0115: ldc.r4 0.5
			IL_011a: ldloc.2
			IL_011b: mul
			IL_011c: ldc.r4 4
			IL_0121: add
			IL_0122: clt
			IL_0124: ldstr "{0:F1} tiles from the middle ({1:F2} tiles a metre)"
			IL_0129: ldloc.3
			IL_012a: box [mscorlib]System.Single
			IL_012f: ldloc.2
			IL_0130: box [mscorlib]System.Single
			IL_0135: call string [mscorlib]System.String::Format(string, object, object)
			IL_013a: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_013f: pop
			IL_0140: ldloc.1
			IL_0141: ldstr "the laser is the mouse, off the screen over the land"
			IL_0146: call bool FirstPerson.Vr.VrMouse::get_Driving()
			IL_014b: brfalse.s IL_0163

			IL_014d: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_0152: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0157: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrMouse::Away
			IL_015c: call bool [UnityEngine.CoreModule]UnityEngine.Vector2::op_Equality(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0161: br.s IL_0164

			IL_0163: ldc.i4.0

			IL_0164: ldstr "driving {0}, mouse {1}"
			IL_0169: call bool FirstPerson.Vr.VrMouse::get_Driving()
			IL_016e: box [mscorlib]System.Boolean
			IL_0173: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.InputLegacyModule]UnityEngine.Input::get_mousePosition()
			IL_0178: box [UnityEngine.CoreModule]UnityEngine.Vector3
			IL_017d: call string [mscorlib]System.String::Format(string, object, object)
			IL_0182: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0187: pop
			IL_0188: ldarg.0
			IL_0189: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.GodView::LandPoint
			IL_018e: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<was>5__2'
			IL_0193: ldloc.1
			IL_0194: ldloca.s 7
			IL_0196: initobj FirstPerson.Vr.Xr/Vec3
			IL_019c: ldloca.s 7
			IL_019e: ldc.r4 0.25
			IL_01a3: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_01a8: ldloca.s 7
			IL_01aa: ldc.r4 0.0
			IL_01af: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_01b4: ldloca.s 7
			IL_01b6: ldc.r4 -1
			IL_01bb: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_01c0: ldloc.s 7
			IL_01c2: ldc.r4 0.15
			IL_01c7: ldc.r4 -0.25
			IL_01cc: ldc.r4 -0.2
			IL_01d1: ldc.i4.1
			IL_01d2: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_01d7: ldarg.0
			IL_01d8: ldloc.1
			IL_01d9: ldloc.1
			IL_01da: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_01df: ldc.r4 20
			IL_01e4: ldc.r4 0.4
			IL_01e9: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_01ee: stfld object FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>2__current'
			IL_01f3: ldarg.0
			IL_01f4: ldc.i4.2
			IL_01f5: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_01fa: ldc.i4.1
			IL_01fb: ret

			IL_01fc: ldarg.0
			IL_01fd: ldc.i4.m1
			IL_01fe: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_0203: call void FirstPerson.GodView::Resolve()
			IL_0208: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.GodView::LandPoint
			IL_020d: ldarg.0
			IL_020e: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<was>5__2'
			IL_0213: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0218: ldloc.1
			IL_0219: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_021e: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_0223: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
			IL_0228: ldloc.1
			IL_0229: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_022e: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_0233: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
			IL_0238: neg
			IL_0239: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_023e: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Dot(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0243: stloc.s 4
			IL_0245: ldloc.1
			IL_0246: ldstr "turning the hand right moves the point right over the land"
			IL_024b: ldloc.s 4
			IL_024d: ldc.r4 1
			IL_0252: cgt
			IL_0254: ldstr "{0:F1} tiles right"
			IL_0259: ldloc.s 4
			IL_025b: box [mscorlib]System.Single
			IL_0260: call string [mscorlib]System.String::Format(string, object)
			IL_0265: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_026a: pop
			IL_026b: ldloc.1
			IL_026c: ldstr "vrui_laser"
			IL_0271: call instance void FirstPerson.VrUiTest::SaveEyes(string)
			IL_0276: ldloc.1
			IL_0277: ldloca.s 7
			IL_0279: initobj FirstPerson.Vr.Xr/Vec3
			IL_027f: ldloca.s 7
			IL_0281: ldc.r4 0.25
			IL_0286: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_028b: ldloca.s 7
			IL_028d: ldc.r4 1.2
			IL_0292: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_0297: ldloca.s 7
			IL_0299: ldc.r4 -1
			IL_029e: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_02a3: ldloc.s 7
			IL_02a5: ldc.r4 0.15
			IL_02aa: ldc.r4 -0.25
			IL_02af: ldc.r4 -0.2
			IL_02b4: ldc.i4.1
			IL_02b5: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_02ba: ldarg.0
			IL_02bb: ldloc.1
			IL_02bc: ldloc.1
			IL_02bd: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_02c2: ldc.r4 20
			IL_02c7: ldc.r4 0.3
			IL_02cc: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_02d1: stfld object FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>2__current'
			IL_02d6: ldarg.0
			IL_02d7: ldc.i4.3
			IL_02d8: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_02dd: ldc.i4.1
			IL_02de: ret

			IL_02df: ldarg.0
			IL_02e0: ldc.i4.m1
			IL_02e1: stfld int32 FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>1__state'
			IL_02e6: call void FirstPerson.GodView::Resolve()
			IL_02eb: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_02f0: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']MapBox::getMousePos()
			IL_02f5: stloc.s 5
			IL_02f7: ldloc.s 5
			IL_02f9: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_02fe: ldc.r4 0.0
			IL_0303: blt.s IL_0336

			IL_0305: ldloc.s 5
			IL_0307: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_030c: ldc.r4 0.0
			IL_0311: blt.s IL_0336

			IL_0313: ldloc.s 5
			IL_0315: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_031a: ldsfld int32 ['Assembly-CSharp']MapBox::width
			IL_031f: conv.r4
			IL_0320: bge.s IL_0336

			IL_0322: ldloc.s 5
			IL_0324: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0329: ldsfld int32 ['Assembly-CSharp']MapBox::height
			IL_032e: conv.r4
			IL_032f: clt.un
			IL_0331: ldc.i4.0
			IL_0332: ceq
			IL_0334: br.s IL_0337

			IL_0336: ldc.i4.1

			IL_0337: stloc.s 6
			IL_0339: ldloc.1
			IL_033a: ldstr "up at the sky the laser is off the land, the game's point off the map"
			IL_033f: ldsfld bool FirstPerson.GodView::MouseValid
			IL_0344: ldc.i4.0
			IL_0345: ceq
			IL_0347: ldloc.s 6
			IL_0349: and
			IL_034a: ldstr "on the land {0}, game point {1:F0},{2:F0}"
			IL_034f: ldsfld bool FirstPerson.GodView::MouseValid
			IL_0354: box [mscorlib]System.Boolean
			IL_0359: ldloc.s 5
			IL_035b: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0360: box [mscorlib]System.Single
			IL_0365: ldloc.s 5
			IL_0367: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_036c: box [mscorlib]System.Single
			IL_0371: call string [mscorlib]System.String::Format(string, object, object, object)
			IL_0376: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_037b: pop
			IL_037c: ldc.i4.0
			IL_037d: ret
		} // end of method '<LaserLand>d__12'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51e50e
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>2__current'
			IL_0006: ret
		} // end of method '<LaserLand>d__12'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51e516
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<LaserLand>d__12'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51e51d
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>2__current'
			IL_0006: ret
		} // end of method '<LaserLand>d__12'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<LaserLand>d__12'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<LaserLand>d__12'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <LaserLand>d__12

	.class nested private auto ansi sealed beforefieldinit '<LaserPower>d__13'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<at>5__2'
		.field private int32 '<before>5__3'
		.field private bool '<down>5__4'
		.field private int32 '<i>5__5'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51e525
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_000d: ret
		} // end of method '<LaserPower>d__13'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51e534
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<LaserPower>d__13'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51e538
			// Header size: 12
			// Code size: 795 (0x31b)
			.maxstack 8
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] int32,
				[3] valuetype FirstPerson.Vr.Xr/Vec3,
				[4] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_0036, IL_00a5, IL_010f, IL_0180, IL_01ea, IL_028f, IL_02a6, IL_02de)

			IL_0034: ldc.i4.0
			IL_0035: ret

			IL_0036: ldarg.0
			IL_0037: ldc.i4.m1
			IL_0038: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_003d: ldloc.1
			IL_003e: ldloca.s 3
			IL_0040: initobj FirstPerson.Vr.Xr/Vec3
			IL_0046: ldloca.s 3
			IL_0048: ldc.r4 -0.08
			IL_004d: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_0052: ldloca.s 3
			IL_0054: ldc.r4 0.0
			IL_0059: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_005e: ldloca.s 3
			IL_0060: ldc.r4 -1
			IL_0065: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_006a: ldloc.3
			IL_006b: ldc.r4 0.15
			IL_0070: ldc.r4 -0.25
			IL_0075: ldc.r4 -0.2
			IL_007a: ldc.i4.1
			IL_007b: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_0080: ldarg.0
			IL_0081: ldloc.1
			IL_0082: ldloc.1
			IL_0083: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0088: ldc.r4 20
			IL_008d: ldc.r4 0.5
			IL_0092: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0097: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_009c: ldarg.0
			IL_009d: ldc.i4.1
			IL_009e: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_00a3: ldc.i4.1
			IL_00a4: ret

			IL_00a5: ldarg.0
			IL_00a6: ldc.i4.m1
			IL_00a7: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_00ac: call void FirstPerson.GodView::Resolve()
			IL_00b1: ldarg.0
			IL_00b2: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.GodView::LandPoint
			IL_00b7: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<at>5__2'
			IL_00bc: ldarg.0
			IL_00bd: ldloc.1
			IL_00be: ldarg.0
			IL_00bf: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<at>5__2'
			IL_00c4: call instance int32 FirstPerson.VrUiTest::Sheep(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_00c9: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<before>5__3'
			IL_00ce: ldstr "sheep"
			IL_00d3: call bool FirstPerson.VrUiTest::Select(string)
			IL_00d8: brtrue.s IL_00ea

			IL_00da: ldloc.1
			IL_00db: ldstr "the sheep power can be picked"
			IL_00e0: ldc.i4.0
			IL_00e1: ldnull
			IL_00e2: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_00e7: pop
			IL_00e8: ldc.i4.0
			IL_00e9: ret

			IL_00ea: ldarg.0
			IL_00eb: ldloc.1
			IL_00ec: ldloc.1
			IL_00ed: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_00f2: ldc.r4 20
			IL_00f7: ldc.r4 0.3
			IL_00fc: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0101: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_0106: ldarg.0
			IL_0107: ldc.i4.2
			IL_0108: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_010d: ldc.i4.1
			IL_010e: ret

			IL_010f: ldarg.0
			IL_0110: ldc.i4.m1
			IL_0111: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_0116: ldloc.1
			IL_0117: ldstr "a power is picked"
			IL_011c: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0121: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_0126: ldstr "sheep"
			IL_012b: call bool [mscorlib]System.String::op_Equality(string, string)
			IL_0130: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0135: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_013a: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_013f: pop
			IL_0140: ldarg.0
			IL_0141: ldc.i4.0
			IL_0142: stfld bool FirstPerson.VrUiTest/'<LaserPower>d__13'::'<down>5__4'
			IL_0147: ldloc.1
			IL_0148: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_014d: ldc.r4 1
			IL_0152: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_0157: ldarg.0
			IL_0158: ldc.i4.0
			IL_0159: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<i>5__5'
			IL_015e: br.s IL_01ac

			IL_0160: ldloc.1
			IL_0161: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0166: ldc.r4 20
			IL_016b: call void FirstPerson.VrUiTest::HoldNow(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
			IL_0170: ldarg.0
			IL_0171: ldnull
			IL_0172: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_0177: ldarg.0
			IL_0178: ldc.i4.3
			IL_0179: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_017e: ldc.i4.1
			IL_017f: ret

			IL_0180: ldarg.0
			IL_0181: ldc.i4.m1
			IL_0182: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_0187: ldarg.0
			IL_0188: ldarg.0
			IL_0189: ldfld bool FirstPerson.VrUiTest/'<LaserPower>d__13'::'<down>5__4'
			IL_018e: ldc.i4.0
			IL_018f: call bool [UnityEngine.InputLegacyModule]UnityEngine.Input::GetMouseButton(int32)
			IL_0194: or
			IL_0195: stfld bool FirstPerson.VrUiTest/'<LaserPower>d__13'::'<down>5__4'
			IL_019a: ldarg.0
			IL_019b: ldfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<i>5__5'
			IL_01a0: stloc.s 4
			IL_01a2: ldarg.0
			IL_01a3: ldloc.s 4
			IL_01a5: ldc.i4.1
			IL_01a6: add
			IL_01a7: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<i>5__5'

			IL_01ac: ldarg.0
			IL_01ad: ldfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<i>5__5'
			IL_01b2: ldc.i4.4
			IL_01b3: blt.s IL_0160

			IL_01b5: ldloc.1
			IL_01b6: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_01bb: ldc.r4 0.0
			IL_01c0: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_01c5: ldarg.0
			IL_01c6: ldloc.1
			IL_01c7: ldloc.1
			IL_01c8: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_01cd: ldc.r4 20
			IL_01d2: ldc.r4 1
			IL_01d7: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_01dc: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_01e1: ldarg.0
			IL_01e2: ldc.i4.4
			IL_01e3: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_01e8: ldc.i4.1
			IL_01e9: ret

			IL_01ea: ldarg.0
			IL_01eb: ldc.i4.m1
			IL_01ec: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_01f1: ldloc.1
			IL_01f2: ldarg.0
			IL_01f3: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<at>5__2'
			IL_01f8: call instance int32 FirstPerson.VrUiTest::Sheep(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_01fd: stloc.2
			IL_01fe: ldloc.1
			IL_01ff: ldstr "the trigger is the game's left button"
			IL_0204: ldarg.0
			IL_0205: ldfld bool FirstPerson.VrUiTest/'<LaserPower>d__13'::'<down>5__4'
			IL_020a: ldnull
			IL_020b: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0210: pop
			IL_0211: ldloc.1
			IL_0212: ldstr "the trigger used the power where the laser points (a sheep there)"
			IL_0217: ldloc.2
			IL_0218: ldarg.0
			IL_0219: ldfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<before>5__3'
			IL_021e: cgt
			IL_0220: ldstr "{0} -> {1} sheep within 6 tiles of {2:F0},{3:F0}"
			IL_0225: ldc.i4.4
			IL_0226: newarr [mscorlib]System.Object
			IL_022b: dup
			IL_022c: ldc.i4.0
			IL_022d: ldarg.0
			IL_022e: ldfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<before>5__3'
			IL_0233: box [mscorlib]System.Int32
			IL_0238: stelem.ref
			IL_0239: dup
			IL_023a: ldc.i4.1
			IL_023b: ldloc.2
			IL_023c: box [mscorlib]System.Int32
			IL_0241: stelem.ref
			IL_0242: dup
			IL_0243: ldc.i4.2
			IL_0244: ldarg.0
			IL_0245: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<at>5__2'
			IL_024a: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_024f: box [mscorlib]System.Single
			IL_0254: stelem.ref
			IL_0255: dup
			IL_0256: ldc.i4.3
			IL_0257: ldarg.0
			IL_0258: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<at>5__2'
			IL_025d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0262: box [mscorlib]System.Single
			IL_0267: stelem.ref
			IL_0268: call string [mscorlib]System.String::Format(string, object[])
			IL_026d: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0272: pop
			IL_0273: ldloc.1
			IL_0274: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0279: ldc.i4.1
			IL_027a: stfld bool FirstPerson.Vr.VrSystem::SimAbility
			IL_027f: ldarg.0
			IL_0280: ldnull
			IL_0281: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_0286: ldarg.0
			IL_0287: ldc.i4.5
			IL_0288: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_028d: ldc.i4.1
			IL_028e: ret

			IL_028f: ldarg.0
			IL_0290: ldc.i4.m1
			IL_0291: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_0296: ldarg.0
			IL_0297: ldnull
			IL_0298: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_029d: ldarg.0
			IL_029e: ldc.i4.6
			IL_029f: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_02a4: ldc.i4.1
			IL_02a5: ret

			IL_02a6: ldarg.0
			IL_02a7: ldc.i4.m1
			IL_02a8: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_02ad: ldloc.1
			IL_02ae: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_02b3: ldc.i4.0
			IL_02b4: stfld bool FirstPerson.Vr.VrSystem::SimAbility
			IL_02b9: ldarg.0
			IL_02ba: ldloc.1
			IL_02bb: ldloc.1
			IL_02bc: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_02c1: ldc.r4 20
			IL_02c6: ldc.r4 0.3
			IL_02cb: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_02d0: stfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_02d5: ldarg.0
			IL_02d6: ldc.i4.7
			IL_02d7: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_02dc: ldc.i4.1
			IL_02dd: ret

			IL_02de: ldarg.0
			IL_02df: ldc.i4.m1
			IL_02e0: stfld int32 FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>1__state'
			IL_02e5: ldloc.1
			IL_02e6: ldstr "B puts the power down"
			IL_02eb: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_02f0: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_02f5: call bool [mscorlib]System.String::IsNullOrEmpty(string)
			IL_02fa: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_02ff: callvirt instance string ['Assembly-CSharp']MapBox::getSelectedPowerID()
			IL_0304: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0309: pop
			.try
			{
				IL_030a: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
				IL_030f: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::unselectAll()
				IL_0314: leave.s IL_0319
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_0316: pop
				IL_0317: leave.s IL_0319
			} // end handler

			IL_0319: ldc.i4.0
			IL_031a: ret
		} // end of method '<LaserPower>d__13'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51e870
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_0006: ret
		} // end of method '<LaserPower>d__13'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51e878
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<LaserPower>d__13'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51e87f
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>2__current'
			IL_0006: ret
		} // end of method '<LaserPower>d__13'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<LaserPower>d__13'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<LaserPower>d__13'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <LaserPower>d__13

	.class nested private auto ansi sealed beforefieldinit '<Looks>d__9'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private float32 '<yaw>5__2'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<mid>5__3'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Color '<below>5__4'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 '<still>5__5'
		.field private float32 '<k>5__6'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51e887
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_000d: ret
		} // end of method '<Looks>d__9'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51e896
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<Looks>d__9'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51e898
			// Header size: 12
			// Code size: 1824 (0x720)
			.maxstack 8
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] valuetype FirstPerson.Gpu.EyeCamera,
				[3] float32,
				[4] valuetype FirstPerson.Vr.Xr/Vec3,
				[5] float32,
				[6] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
				[7] valuetype [UnityEngine.CoreModule]UnityEngine.Color,
				[8] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
				[9] float32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Looks>d__9'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_0036, IL_00a1, IL_015e, IL_0244, IL_0407, IL_0531, IL_05b3, IL_0717)

			IL_0034: ldc.i4.0
			IL_0035: ret

			IL_0036: ldarg.0
			IL_0037: ldc.i4.m1
			IL_0038: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_003d: ldarg.0
			IL_003e: ldloc.1
			IL_003f: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0044: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_0049: stfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<yaw>5__2'
			IL_004e: ldloc.1
			IL_004f: ldc.r4 0.0
			IL_0054: ldc.r4 1
			IL_0059: ldc.r4 0.0
			IL_005e: ldc.r4 90
			IL_0063: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_0068: ldc.r4 0.0
			IL_006d: ldc.r4 0.0
			IL_0072: ldc.r4 0.0
			IL_0077: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_007c: ldarg.0
			IL_007d: ldloc.1
			IL_007e: ldloc.1
			IL_007f: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0084: ldc.r4 20
			IL_0089: ldc.r4 0.5
			IL_008e: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0093: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_0098: ldarg.0
			IL_0099: ldc.i4.1
			IL_009a: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_009f: ldc.i4.1
			IL_00a0: ret

			IL_00a1: ldarg.0
			IL_00a2: ldc.i4.m1
			IL_00a3: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_00a8: ldloc.1
			IL_00a9: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_00ae: ldc.i4.0
			IL_00af: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_00b4: stloc.2
			IL_00b5: ldloc.2
			IL_00b6: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Forward
			IL_00bb: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_00c0: ldloc.2
			IL_00c1: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Forward
			IL_00c6: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_00cb: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Atan2(float32, float32)
			IL_00d0: stloc.3
			IL_00d1: ldloc.1
			IL_00d2: ldstr "turning the head 90 degrees left looks 90 degrees left over the land"
			IL_00d7: ldloc.3
			IL_00d8: ldc.r4 57.29578
			IL_00dd: mul
			IL_00de: ldarg.0
			IL_00df: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<yaw>5__2'
			IL_00e4: ldc.r4 57.29578
			IL_00e9: mul
			IL_00ea: ldc.r4 90
			IL_00ef: add
			IL_00f0: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::DeltaAngle(float32, float32)
			IL_00f5: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_00fa: ldc.r4 2
			IL_00ff: clt
			IL_0101: ldstr "{0:F1} deg"
			IL_0106: ldarg.0
			IL_0107: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<yaw>5__2'
			IL_010c: ldc.r4 57.29578
			IL_0111: mul
			IL_0112: ldloc.3
			IL_0113: ldc.r4 57.29578
			IL_0118: mul
			IL_0119: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::DeltaAngle(float32, float32)
			IL_011e: box [mscorlib]System.Single
			IL_0123: call string [mscorlib]System.String::Format(string, object)
			IL_0128: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_012d: pop
			IL_012e: ldloc.1
			IL_012f: ldstr "vrui_god_left"
			IL_0134: call instance void FirstPerson.VrUiTest::SaveEyes(string)
			IL_0139: ldarg.0
			IL_013a: ldloc.1
			IL_013b: ldloc.1
			IL_013c: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0141: ldc.r4 20
			IL_0146: ldc.r4 1.6
			IL_014b: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0150: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_0155: ldarg.0
			IL_0156: ldc.i4.2
			IL_0157: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_015c: ldc.i4.1
			IL_015d: ret

			IL_015e: ldarg.0
			IL_015f: ldc.i4.m1
			IL_0160: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_0165: ldloc.1
			IL_0166: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_016b: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_0170: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.Vr.VrSystem/Layers::PanelPose
			IL_0175: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_017a: stloc.s 4
			IL_017c: ldloc.s 4
			IL_017e: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_0183: neg
			IL_0184: ldloc.s 4
			IL_0186: ldfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_018b: neg
			IL_018c: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Atan2(float32, float32)
			IL_0191: ldc.r4 57.29578
			IL_0196: mul
			IL_0197: stloc.s 5
			IL_0199: ldloc.1
			IL_019a: ldstr "looking well away over the map, the controls come round in front of you"
			IL_019f: ldloc.s 5
			IL_01a1: ldc.r4 90
			IL_01a6: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::DeltaAngle(float32, float32)
			IL_01ab: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_01b0: ldc.r4 5
			IL_01b5: clt
			IL_01b7: ldstr "controls at {0:F1} deg (head at 90)"
			IL_01bc: ldloc.s 5
			IL_01be: box [mscorlib]System.Single
			IL_01c3: call string [mscorlib]System.String::Format(string, object)
			IL_01c8: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_01cd: pop
			IL_01ce: ldarg.0
			IL_01cf: ldsfld int32 ['Assembly-CSharp']MapBox::width
			IL_01d4: conv.r4
			IL_01d5: ldc.r4 0.5
			IL_01da: mul
			IL_01db: ldsfld int32 ['Assembly-CSharp']MapBox::height
			IL_01e0: conv.r4
			IL_01e1: ldc.r4 0.5
			IL_01e6: mul
			IL_01e7: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_01ec: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Looks>d__9'::'<mid>5__3'
			IL_01f1: ldloc.1
			IL_01f2: ldc.r4 1
			IL_01f7: ldc.r4 0.0
			IL_01fc: ldc.r4 0.0
			IL_0201: ldc.r4 -40
			IL_0206: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_020b: ldc.r4 0.0
			IL_0210: ldc.r4 0.0
			IL_0215: ldc.r4 0.0
			IL_021a: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_021f: ldarg.0
			IL_0220: ldloc.1
			IL_0221: ldarg.0
			IL_0222: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Looks>d__9'::'<mid>5__3'
			IL_0227: ldc.r4 20
			IL_022c: ldc.r4 0.8
			IL_0231: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0236: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_023b: ldarg.0
			IL_023c: ldc.i4.3
			IL_023d: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_0242: ldc.i4.1
			IL_0243: ret

			IL_0244: ldarg.0
			IL_0245: ldc.i4.m1
			IL_0246: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_024b: ldloc.1
			IL_024c: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0251: ldc.i4.0
			IL_0252: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_0257: stloc.2
			IL_0258: ldloc.1
			IL_0259: ldstr "looking down, the eyes look down"
			IL_025e: ldloc.2
			IL_025f: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Forward
			IL_0264: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_0269: ldc.r4 -0.95
			IL_026e: clt
			IL_0270: ldstr "forward z {0:F3}"
			IL_0275: ldloc.2
			IL_0276: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Forward
			IL_027b: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_0280: box [mscorlib]System.Single
			IL_0285: call string [mscorlib]System.String::Format(string, object)
			IL_028a: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_028f: pop
			IL_0290: ldarg.0
			IL_0291: ldloc.1
			IL_0292: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0297: ldc.i4.0
			IL_0298: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::VrEyeForTest(int32)
			IL_029d: call valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest::Middle(class [UnityEngine.CoreModule]UnityEngine.RenderTexture)
			IL_02a2: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_02a7: ldloc.1
			IL_02a8: ldstr "looking down shows what is under you (not black)"
			IL_02ad: ldarg.0
			IL_02ae: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_02b3: call instance float32 [UnityEngine.CoreModule]UnityEngine.Color::get_maxColorComponent()
			IL_02b8: ldc.r4 0.08
			IL_02bd: cgt
			IL_02bf: ldarg.0
			IL_02c0: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_02c5: call string FirstPerson.VrUiTest::Show(valuetype [UnityEngine.CoreModule]UnityEngine.Color)
			IL_02ca: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_02cf: pop
			IL_02d0: ldloc.1
			IL_02d1: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02d6: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.WorldBoxMod::get_VrGodSeatForTest()
			IL_02db: stloc.s 6
			IL_02dd: ldloc.s 6
			IL_02df: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_02e4: ldc.r4 4
			IL_02e9: blt.s IL_0323

			IL_02eb: ldloc.s 6
			IL_02ed: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_02f2: ldc.r4 4
			IL_02f7: blt.s IL_0323

			IL_02f9: ldloc.s 6
			IL_02fb: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_0300: ldsfld int32 ['Assembly-CSharp']MapBox::width
			IL_0305: conv.r4
			IL_0306: ldc.r4 4
			IL_030b: sub
			IL_030c: bgt.s IL_0323

			IL_030e: ldloc.s 6
			IL_0310: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_0315: ldsfld int32 ['Assembly-CSharp']MapBox::height
			IL_031a: conv.r4
			IL_031b: ldc.r4 4
			IL_0320: sub
			IL_0321: ble.un.s IL_034d

			IL_0323: ldloc.1
			IL_0324: ldstr "hovering past the map's edge ({0:F0},{1:F0}): the seam check skipped"
			IL_0329: ldloc.s 6
			IL_032b: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_0330: box [mscorlib]System.Single
			IL_0335: ldloc.s 6
			IL_0337: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_033c: box [mscorlib]System.Single
			IL_0341: call string [mscorlib]System.String::Format(string, object, object)
			IL_0346: call instance void FirstPerson.CivilTest::Note(string)
			IL_034b: br.s IL_03a9

			IL_034d: ldloc.1
			IL_034e: ldstr "no seam right under you (the rows round the middle alike)"
			IL_0353: ldloc.1
			IL_0354: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0359: ldc.i4.0
			IL_035a: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::VrEyeForTest(int32)
			IL_035f: call float32 FirstPerson.VrUiTest::SeamUnder(class [UnityEngine.CoreModule]UnityEngine.RenderTexture)
			IL_0364: ldc.r4 0.06
			IL_0369: clt
			IL_036b: ldstr "{0:F3} over {1:F0},{2:F0}"
			IL_0370: ldloc.1
			IL_0371: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0376: ldc.i4.0
			IL_0377: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::VrEyeForTest(int32)
			IL_037c: call float32 FirstPerson.VrUiTest::SeamUnder(class [UnityEngine.CoreModule]UnityEngine.RenderTexture)
			IL_0381: box [mscorlib]System.Single
			IL_0386: ldloc.s 6
			IL_0388: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_038d: box [mscorlib]System.Single
			IL_0392: ldloc.s 6
			IL_0394: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_0399: box [mscorlib]System.Single
			IL_039e: call string [mscorlib]System.String::Format(string, object, object, object)
			IL_03a3: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_03a8: pop

			IL_03a9: ldloc.1
			IL_03aa: ldstr "vrui_god_down"
			IL_03af: call instance void FirstPerson.VrUiTest::SaveEyes(string)
			IL_03b4: ldloc.1
			IL_03b5: ldc.r4 1
			IL_03ba: ldc.r4 0.0
			IL_03bf: ldc.r4 0.0
			IL_03c4: ldc.r4 75
			IL_03c9: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_03ce: ldc.r4 0.0
			IL_03d3: ldc.r4 0.0
			IL_03d8: ldc.r4 0.0
			IL_03dd: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_03e2: ldarg.0
			IL_03e3: ldloc.1
			IL_03e4: ldarg.0
			IL_03e5: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Looks>d__9'::'<mid>5__3'
			IL_03ea: ldc.r4 20
			IL_03ef: ldc.r4 0.5
			IL_03f4: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_03f9: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_03fe: ldarg.0
			IL_03ff: ldc.i4.4
			IL_0400: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_0405: ldc.i4.1
			IL_0406: ret

			IL_0407: ldarg.0
			IL_0408: ldc.i4.m1
			IL_0409: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_040e: ldloc.1
			IL_040f: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0414: ldc.i4.0
			IL_0415: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::VrEyeForTest(int32)
			IL_041a: call valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest::Middle(class [UnityEngine.CoreModule]UnityEngine.RenderTexture)
			IL_041f: stloc.s 7
			IL_0421: ldloc.1
			IL_0422: ldstr "looking up shows the sky"
			IL_0427: ldloc.s 7
			IL_0429: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::b
			IL_042e: ldloc.s 7
			IL_0430: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::r
			IL_0435: blt.un.s IL_0447

			IL_0437: ldloca.s 7
			IL_0439: call instance float32 [UnityEngine.CoreModule]UnityEngine.Color::get_maxColorComponent()
			IL_043e: ldc.r4 0.2
			IL_0443: cgt
			IL_0445: br.s IL_0448

			IL_0447: ldc.i4.0

			IL_0448: ldloc.s 7
			IL_044a: call string FirstPerson.VrUiTest::Show(valuetype [UnityEngine.CoreModule]UnityEngine.Color)
			IL_044f: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0454: pop
			IL_0455: ldloc.1
			IL_0456: ldstr "the sky up there isn't the land down there"
			IL_045b: ldloc.s 7
			IL_045d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::r
			IL_0462: ldarg.0
			IL_0463: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_0468: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::r
			IL_046d: sub
			IL_046e: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_0473: ldloc.s 7
			IL_0475: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::g
			IL_047a: ldarg.0
			IL_047b: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_0480: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::g
			IL_0485: sub
			IL_0486: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_048b: add
			IL_048c: ldloc.s 7
			IL_048e: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::b
			IL_0493: ldarg.0
			IL_0494: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_0499: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::b
			IL_049e: sub
			IL_049f: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_04a4: add
			IL_04a5: ldc.r4 0.1
			IL_04aa: cgt
			IL_04ac: ldstr "up "
			IL_04b1: ldloc.s 7
			IL_04b3: call string FirstPerson.VrUiTest::Show(valuetype [UnityEngine.CoreModule]UnityEngine.Color)
			IL_04b8: ldstr ", down "
			IL_04bd: ldarg.0
			IL_04be: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Color FirstPerson.VrUiTest/'<Looks>d__9'::'<below>5__4'
			IL_04c3: call string FirstPerson.VrUiTest::Show(valuetype [UnityEngine.CoreModule]UnityEngine.Color)
			IL_04c8: call string [mscorlib]System.String::Concat(string, string, string, string)
			IL_04cd: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_04d2: pop
			IL_04d3: ldloc.1
			IL_04d4: ldstr "vrui_god_up"
			IL_04d9: call instance void FirstPerson.VrUiTest::SaveEyes(string)
			IL_04de: ldloc.1
			IL_04df: ldc.r4 0.0
			IL_04e4: ldc.r4 1
			IL_04e9: ldc.r4 0.0
			IL_04ee: ldc.r4 0.0
			IL_04f3: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_04f8: ldc.r4 0.0
			IL_04fd: ldc.r4 0.0
			IL_0502: ldc.r4 0.0
			IL_0507: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_050c: ldarg.0
			IL_050d: ldloc.1
			IL_050e: ldloc.1
			IL_050f: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0514: ldc.r4 20
			IL_0519: ldc.r4 0.4
			IL_051e: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0523: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_0528: ldarg.0
			IL_0529: ldc.i4.5
			IL_052a: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_052f: ldc.i4.1
			IL_0530: ret

			IL_0531: ldarg.0
			IL_0532: ldc.i4.m1
			IL_0533: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_0538: ldarg.0
			IL_0539: ldloc.1
			IL_053a: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_053f: ldc.i4.0
			IL_0540: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_0545: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Position
			IL_054a: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Looks>d__9'::'<still>5__5'
			IL_054f: ldarg.0
			IL_0550: ldloc.1
			IL_0551: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0556: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodScaleForTest()
			IL_055b: stfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<k>5__6'
			IL_0560: ldloc.1
			IL_0561: ldc.r4 0.0
			IL_0566: ldc.r4 1
			IL_056b: ldc.r4 0.0
			IL_0570: ldc.r4 0.0
			IL_0575: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_057a: ldc.r4 0.0
			IL_057f: ldc.r4 0.0
			IL_0584: ldc.r4 -0.5
			IL_0589: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_058e: ldarg.0
			IL_058f: ldloc.1
			IL_0590: ldloc.1
			IL_0591: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_0596: ldc.r4 20
			IL_059b: ldc.r4 0.4
			IL_05a0: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_05a5: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_05aa: ldarg.0
			IL_05ab: ldc.i4.6
			IL_05ac: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_05b1: ldc.i4.1
			IL_05b2: ret

			IL_05b3: ldarg.0
			IL_05b4: ldc.i4.m1
			IL_05b5: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_05ba: ldloc.1
			IL_05bb: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_05c0: ldc.i4.0
			IL_05c1: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_05c6: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Position
			IL_05cb: ldarg.0
			IL_05cc: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Looks>d__9'::'<still>5__5'
			IL_05d1: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_05d6: stloc.s 8
			IL_05d8: ldloc.s 8
			IL_05da: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_05df: ldloc.s 8
			IL_05e1: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_05e6: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_05eb: ldloc.1
			IL_05ec: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_05f1: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_05f6: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
			IL_05fb: ldloc.1
			IL_05fc: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0601: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_0606: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
			IL_060b: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_0610: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Dot(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0615: stloc.s 9
			IL_0617: ldloc.1
			IL_0618: ldstr "leaning in half a metre moves you in at the world's scale"
			IL_061d: ldloca.s 8
			IL_061f: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector3::get_magnitude()
			IL_0624: ldc.r4 0.5
			IL_0629: ldarg.0
			IL_062a: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<k>5__6'
			IL_062f: mul
			IL_0630: sub
			IL_0631: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_0636: ldc.r4 0.1
			IL_063b: ldarg.0
			IL_063c: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<k>5__6'
			IL_0641: mul
			IL_0642: bge.un.s IL_0656

			IL_0644: ldloc.s 9
			IL_0646: ldc.r4 0.25
			IL_064b: ldarg.0
			IL_064c: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<k>5__6'
			IL_0651: mul
			IL_0652: cgt
			IL_0654: br.s IL_0657

			IL_0656: ldc.i4.0

			IL_0657: ldstr "{0:F2} tiles ({1:F2} ahead), want {2:F2}"
			IL_065c: ldloca.s 8
			IL_065e: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector3::get_magnitude()
			IL_0663: box [mscorlib]System.Single
			IL_0668: ldloc.s 9
			IL_066a: box [mscorlib]System.Single
			IL_066f: ldc.r4 0.5
			IL_0674: ldarg.0
			IL_0675: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<k>5__6'
			IL_067a: mul
			IL_067b: box [mscorlib]System.Single
			IL_0680: call string [mscorlib]System.String::Format(string, object, object, object)
			IL_0685: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_068a: pop
			IL_068b: ldloc.1
			IL_068c: ldstr "...in and down towards the land, as the view is tipped"
			IL_0691: ldloc.s 8
			IL_0693: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_0698: neg
			IL_0699: ldc.r4 0.12
			IL_069e: ldarg.0
			IL_069f: ldfld float32 FirstPerson.VrUiTest/'<Looks>d__9'::'<k>5__6'
			IL_06a4: mul
			IL_06a5: cgt
			IL_06a7: ldstr "{0:F2} tiles down"
			IL_06ac: ldloc.s 8
			IL_06ae: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_06b3: neg
			IL_06b4: box [mscorlib]System.Single
			IL_06b9: call string [mscorlib]System.String::Format(string, object)
			IL_06be: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_06c3: pop
			IL_06c4: ldloc.1
			IL_06c5: ldc.r4 0.0
			IL_06ca: ldc.r4 1
			IL_06cf: ldc.r4 0.0
			IL_06d4: ldc.r4 0.0
			IL_06d9: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_06de: ldc.r4 0.0
			IL_06e3: ldc.r4 0.0
			IL_06e8: ldc.r4 0.0
			IL_06ed: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_06f2: ldarg.0
			IL_06f3: ldloc.1
			IL_06f4: ldloc.1
			IL_06f5: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_06fa: ldc.r4 20
			IL_06ff: ldc.r4 0.3
			IL_0704: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0709: stfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_070e: ldarg.0
			IL_070f: ldc.i4.7
			IL_0710: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_0715: ldc.i4.1
			IL_0716: ret

			IL_0717: ldarg.0
			IL_0718: ldc.i4.m1
			IL_0719: stfld int32 FirstPerson.VrUiTest/'<Looks>d__9'::'<>1__state'
			IL_071e: ldc.i4.0
			IL_071f: ret
		} // end of method '<Looks>d__9'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51efc4
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_0006: ret
		} // end of method '<Looks>d__9'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51efcc
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<Looks>d__9'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51efd3
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Looks>d__9'::'<>2__current'
			IL_0006: ret
		} // end of method '<Looks>d__9'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<Looks>d__9'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<Looks>d__9'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <Looks>d__9

	.class nested private auto ansi sealed beforefieldinit '<Sticks>d__15'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field private class [UnityEngine.CoreModule]UnityEngine.Camera '<cam>5__2'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<from>5__3'
		.field private float32 '<yaw>5__4'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 '<you>5__5'
		.field private float32 '<turn>5__6'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<start>5__7'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<before>5__8'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<lastLand>5__9'
		.field private float32 '<flung>5__10'
		.field private float32 '<zoom>5__11'
		.field private int32 '<i>5__12'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51efdb
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_000d: ret
		} // end of method '<Sticks>d__15'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51efea
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<Sticks>d__15'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51efec
			// Header size: 12
			// Code size: 2061 (0x80d)
			.maxstack 8
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[3] float32,
				[4] float32,
				[5] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
				[6] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[7] float32,
				[8] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[9] valuetype FirstPerson.Vr.Xr/Vec3,
				[10] int32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Sticks>d__15'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_005a, IL_00c4, IL_012a, IL_015a, IL_020c, IL_026e, IL_029e, IL_03f4, IL_043f, IL_04ac, IL_04e9, IL_057e, IL_0671, IL_0713, IL_075e, IL_07d7, IL_0804)

			IL_0058: ldc.i4.0
			IL_0059: ret

			IL_005a: ldarg.0
			IL_005b: ldc.i4.m1
			IL_005c: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0061: ldarg.0
			IL_0062: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0067: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Camera ['Assembly-CSharp']MapBox::get_camera()
			IL_006c: stfld class [UnityEngine.CoreModule]UnityEngine.Camera FirstPerson.VrUiTest/'<Sticks>d__15'::'<cam>5__2'
			IL_0071: ldloc.1
			IL_0072: ldc.r4 0.0
			IL_0077: ldc.r4 1
			IL_007c: ldc.r4 0.0
			IL_0081: ldc.r4 0.0
			IL_0086: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_008b: ldc.r4 0.0
			IL_0090: ldc.r4 0.0
			IL_0095: ldc.r4 0.0
			IL_009a: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_009f: ldarg.0
			IL_00a0: ldloc.1
			IL_00a1: ldloc.1
			IL_00a2: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_00a7: ldc.r4 20
			IL_00ac: ldc.r4 0.6
			IL_00b1: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_00b6: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_00bb: ldarg.0
			IL_00bc: ldc.i4.1
			IL_00bd: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_00c2: ldc.i4.1
			IL_00c3: ret

			IL_00c4: ldarg.0
			IL_00c5: ldc.i4.m1
			IL_00c6: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_00cb: ldarg.0
			IL_00cc: ldarg.0
			IL_00cd: ldfld class [UnityEngine.CoreModule]UnityEngine.Camera FirstPerson.VrUiTest/'<Sticks>d__15'::'<cam>5__2'
			IL_00d2: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_00d7: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
			IL_00dc: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_00e1: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<from>5__3'
			IL_00e6: ldarg.0
			IL_00e7: ldloc.1
			IL_00e8: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_00ed: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrGodYawForTest()
			IL_00f2: stfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<yaw>5__4'
			IL_00f7: ldloc.1
			IL_00f8: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_00fd: ldc.r4 0.0
			IL_0102: ldc.r4 1
			IL_0107: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_010c: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimMove
			IL_0111: ldarg.0
			IL_0112: ldc.r4 1
			IL_0117: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_011c: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0121: ldarg.0
			IL_0122: ldc.i4.2
			IL_0123: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0128: ldc.i4.1
			IL_0129: ret

			IL_012a: ldarg.0
			IL_012b: ldc.i4.m1
			IL_012c: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0131: ldloc.1
			IL_0132: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0137: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_zero()
			IL_013c: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimMove
			IL_0141: ldarg.0
			IL_0142: ldc.r4 0.6
			IL_0147: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_014c: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0151: ldarg.0
			IL_0152: ldc.i4.3
			IL_0153: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0158: ldc.i4.1
			IL_0159: ret

			IL_015a: ldarg.0
			IL_015b: ldc.i4.m1
			IL_015c: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0161: ldarg.0
			IL_0162: ldfld class [UnityEngine.CoreModule]UnityEngine.Camera FirstPerson.VrUiTest/'<Sticks>d__15'::'<cam>5__2'
			IL_0167: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_016c: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
			IL_0171: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0176: ldarg.0
			IL_0177: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<from>5__3'
			IL_017c: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0181: stloc.2
			IL_0182: ldloc.2
			IL_0183: ldarg.0
			IL_0184: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<yaw>5__4'
			IL_0189: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
			IL_018e: ldarg.0
			IL_018f: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<yaw>5__4'
			IL_0194: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
			IL_0199: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_019e: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Dot(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_01a3: stloc.3
			IL_01a4: ldloc.1
			IL_01a5: ldstr "the left stick moves you the way you look"
			IL_01aa: ldloc.3
			IL_01ab: ldc.r4 2
			IL_01b0: ble.un.s IL_01c4

			IL_01b2: ldloc.3
			IL_01b3: ldloca.s 2
			IL_01b5: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector2::get_magnitude()
			IL_01ba: ldc.r4 0.8
			IL_01bf: mul
			IL_01c0: cgt
			IL_01c2: br.s IL_01c5

			IL_01c4: ldc.i4.0

			IL_01c5: ldstr "{0:F1} tiles, {1:F1} ahead"
			IL_01ca: ldloca.s 2
			IL_01cc: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector2::get_magnitude()
			IL_01d1: box [mscorlib]System.Single
			IL_01d6: ldloc.3
			IL_01d7: box [mscorlib]System.Single
			IL_01dc: call string [mscorlib]System.String::Format(string, object, object)
			IL_01e1: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_01e6: pop
			IL_01e7: ldarg.0
			IL_01e8: ldloc.1
			IL_01e9: ldloc.1
			IL_01ea: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_01ef: ldc.r4 20
			IL_01f4: ldc.r4 0.6
			IL_01f9: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_01fe: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0203: ldarg.0
			IL_0204: ldc.i4.4
			IL_0205: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_020a: ldc.i4.1
			IL_020b: ret

			IL_020c: ldarg.0
			IL_020d: ldc.i4.m1
			IL_020e: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0213: ldarg.0
			IL_0214: ldloc.1
			IL_0215: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_021a: ldc.i4.0
			IL_021b: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_0220: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Position
			IL_0225: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Sticks>d__15'::'<you>5__5'
			IL_022a: ldarg.0
			IL_022b: ldloc.1
			IL_022c: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0231: callvirt instance float32 FirstPerson.WorldBoxMod::get_GodTurnForTest()
			IL_0236: stfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<turn>5__6'
			IL_023b: ldloc.1
			IL_023c: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0241: ldc.r4 1
			IL_0246: ldc.r4 0.0
			IL_024b: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_0250: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimTurn
			IL_0255: ldarg.0
			IL_0256: ldc.r4 0.5
			IL_025b: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0260: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0265: ldarg.0
			IL_0266: ldc.i4.5
			IL_0267: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_026c: ldc.i4.1
			IL_026d: ret

			IL_026e: ldarg.0
			IL_026f: ldc.i4.m1
			IL_0270: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0275: ldloc.1
			IL_0276: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_027b: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_zero()
			IL_0280: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimTurn
			IL_0285: ldarg.0
			IL_0286: ldc.r4 0.5
			IL_028b: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0290: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0295: ldarg.0
			IL_0296: ldc.i4.6
			IL_0297: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_029c: ldc.i4.1
			IL_029d: ret

			IL_029e: ldarg.0
			IL_029f: ldc.i4.m1
			IL_02a0: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_02a5: ldarg.0
			IL_02a6: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<turn>5__6'
			IL_02ab: ldc.r4 57.29578
			IL_02b0: mul
			IL_02b1: ldloc.1
			IL_02b2: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02b7: callvirt instance float32 FirstPerson.WorldBoxMod::get_GodTurnForTest()
			IL_02bc: ldc.r4 57.29578
			IL_02c1: mul
			IL_02c2: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::DeltaAngle(float32, float32)
			IL_02c7: stloc.s 4
			IL_02c9: ldloc.1
			IL_02ca: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02cf: ldc.i4.0
			IL_02d0: callvirt instance valuetype FirstPerson.Gpu.EyeCamera FirstPerson.WorldBoxMod::VrEyeCameraForTest(int32)
			IL_02d5: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.Gpu.EyeCamera::Position
			IL_02da: stloc.s 5
			IL_02dc: ldloc.1
			IL_02dd: ldstr "the right stick snaps a turn to the right"
			IL_02e2: ldloc.s 4
			IL_02e4: ldc.r4 30
			IL_02e9: add
			IL_02ea: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_02ef: ldc.r4 1
			IL_02f4: clt
			IL_02f6: ldstr "{0:F1} deg"
			IL_02fb: ldloc.s 4
			IL_02fd: box [mscorlib]System.Single
			IL_0302: call string [mscorlib]System.String::Format(string, object)
			IL_0307: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_030c: pop
			IL_030d: ldloc.1
			IL_030e: ldstr "turning, you stay where you are (the land turns round you)"
			IL_0313: ldloc.s 5
			IL_0315: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_031a: ldarg.0
			IL_031b: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Sticks>d__15'::'<you>5__5'
			IL_0320: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_0325: sub
			IL_0326: ldloc.s 5
			IL_0328: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_032d: ldarg.0
			IL_032e: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Sticks>d__15'::'<you>5__5'
			IL_0333: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_0338: sub
			IL_0339: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_033e: stloc.s 8
			IL_0340: ldloca.s 8
			IL_0342: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector2::get_magnitude()
			IL_0347: ldc.r4 1
			IL_034c: clt
			IL_034e: ldstr "{0:F2} tiles"
			IL_0353: ldloc.s 5
			IL_0355: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_035a: ldarg.0
			IL_035b: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Sticks>d__15'::'<you>5__5'
			IL_0360: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
			IL_0365: sub
			IL_0366: ldloc.s 5
			IL_0368: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_036d: ldarg.0
			IL_036e: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 FirstPerson.VrUiTest/'<Sticks>d__15'::'<you>5__5'
			IL_0373: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
			IL_0378: sub
			IL_0379: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_037e: stloc.s 8
			IL_0380: ldloca.s 8
			IL_0382: call instance float32 [UnityEngine.CoreModule]UnityEngine.Vector2::get_magnitude()
			IL_0387: box [mscorlib]System.Single
			IL_038c: call string [mscorlib]System.String::Format(string, object)
			IL_0391: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0396: pop
			IL_0397: ldloc.1
			IL_0398: ldloca.s 9
			IL_039a: initobj FirstPerson.Vr.Xr/Vec3
			IL_03a0: ldloca.s 9
			IL_03a2: ldc.r4 0.0
			IL_03a7: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_03ac: ldloca.s 9
			IL_03ae: ldc.r4 0.0
			IL_03b3: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_03b8: ldloca.s 9
			IL_03ba: ldc.r4 -1
			IL_03bf: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_03c4: ldloc.s 9
			IL_03c6: ldc.r4 0.15
			IL_03cb: ldc.r4 -0.25
			IL_03d0: ldc.r4 -0.2
			IL_03d5: ldc.i4.1
			IL_03d6: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_03db: ldarg.0
			IL_03dc: ldc.r4 0.4
			IL_03e1: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_03e6: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_03eb: ldarg.0
			IL_03ec: ldc.i4.7
			IL_03ed: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_03f2: ldc.i4.1
			IL_03f3: ret

			IL_03f4: ldarg.0
			IL_03f5: ldc.i4.m1
			IL_03f6: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_03fb: ldarg.0
			IL_03fc: ldarg.0
			IL_03fd: ldfld class [UnityEngine.CoreModule]UnityEngine.Camera FirstPerson.VrUiTest/'<Sticks>d__15'::'<cam>5__2'
			IL_0402: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_0407: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
			IL_040c: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0411: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<start>5__7'
			IL_0416: ldloc.1
			IL_0417: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_041c: ldc.r4 1
			IL_0421: stfld float32 FirstPerson.Vr.VrSystem::SimUse
			IL_0426: ldarg.0
			IL_0427: ldc.r4 0.2
			IL_042c: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0431: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0436: ldarg.0
			IL_0437: ldc.i4.8
			IL_0438: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_043d: ldc.i4.1
			IL_043e: ret

			IL_043f: ldarg.0
			IL_0440: ldc.i4.m1
			IL_0441: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0446: ldarg.0
			IL_0447: ldc.i4.1
			IL_0448: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<i>5__12'
			IL_044d: br.s IL_04c5

			IL_044f: ldloc.1
			IL_0450: ldloca.s 9
			IL_0452: initobj FirstPerson.Vr.Xr/Vec3
			IL_0458: ldloca.s 9
			IL_045a: ldc.r4 0.04
			IL_045f: ldarg.0
			IL_0460: ldfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<i>5__12'
			IL_0465: conv.r4
			IL_0466: mul
			IL_0467: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_046c: ldloca.s 9
			IL_046e: ldc.r4 0.0
			IL_0473: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_0478: ldloca.s 9
			IL_047a: ldc.r4 -1
			IL_047f: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_0484: ldloc.s 9
			IL_0486: ldc.r4 0.15
			IL_048b: ldc.r4 -0.25
			IL_0490: ldc.r4 -0.2
			IL_0495: ldc.i4.1
			IL_0496: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_049b: ldarg.0
			IL_049c: ldnull
			IL_049d: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_04a2: ldarg.0
			IL_04a3: ldc.i4.s 9
			IL_04a5: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_04aa: ldc.i4.1
			IL_04ab: ret

			IL_04ac: ldarg.0
			IL_04ad: ldc.i4.m1
			IL_04ae: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_04b3: ldarg.0
			IL_04b4: ldfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<i>5__12'
			IL_04b9: stloc.s 10
			IL_04bb: ldarg.0
			IL_04bc: ldloc.s 10
			IL_04be: ldc.i4.1
			IL_04bf: add
			IL_04c0: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<i>5__12'

			IL_04c5: ldarg.0
			IL_04c6: ldfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<i>5__12'
			IL_04cb: ldc.i4.s 10
			IL_04cd: ble.s IL_044f

			IL_04cf: ldarg.0
			IL_04d0: ldc.r4 0.3
			IL_04d5: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_04da: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_04df: ldarg.0
			IL_04e0: ldc.i4.s 10
			IL_04e2: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_04e7: ldc.i4.1
			IL_04e8: ret

			IL_04e9: ldarg.0
			IL_04ea: ldc.i4.m1
			IL_04eb: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_04f0: ldarg.0
			IL_04f1: ldarg.0
			IL_04f2: ldfld class [UnityEngine.CoreModule]UnityEngine.Camera FirstPerson.VrUiTest/'<Sticks>d__15'::'<cam>5__2'
			IL_04f7: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_04fc: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
			IL_0501: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0506: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<before>5__8'
			IL_050b: call void FirstPerson.GodView::Resolve()
			IL_0510: ldarg.0
			IL_0511: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0516: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']MapBox::getMousePos()
			IL_051b: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<lastLand>5__9'
			IL_0520: ldloc.1
			IL_0521: ldloca.s 9
			IL_0523: initobj FirstPerson.Vr.Xr/Vec3
			IL_0529: ldloca.s 9
			IL_052b: ldc.r4 0.4
			IL_0530: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_0535: ldloca.s 9
			IL_0537: ldc.r4 1.5
			IL_053c: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_0541: ldloca.s 9
			IL_0543: ldc.r4 -1
			IL_0548: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_054d: ldloc.s 9
			IL_054f: ldc.r4 0.15
			IL_0554: ldc.r4 -0.25
			IL_0559: ldc.r4 -0.2
			IL_055e: ldc.i4.1
			IL_055f: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_0564: ldarg.0
			IL_0565: ldc.r4 0.4
			IL_056a: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_056f: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0574: ldarg.0
			IL_0575: ldc.i4.s 11
			IL_0577: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_057c: ldc.i4.1
			IL_057d: ret

			IL_057e: ldarg.0
			IL_057f: ldc.i4.m1
			IL_0580: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0585: ldarg.0
			IL_0586: ldarg.0
			IL_0587: ldfld class [UnityEngine.CoreModule]UnityEngine.Camera FirstPerson.VrUiTest/'<Sticks>d__15'::'<cam>5__2'
			IL_058c: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_0591: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
			IL_0596: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::op_Implicit(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_059b: ldarg.0
			IL_059c: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<before>5__8'
			IL_05a1: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_05a6: stfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<flung>5__10'
			IL_05ab: call void FirstPerson.GodView::Resolve()
			IL_05b0: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_05b5: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']MapBox::getMousePos()
			IL_05ba: stloc.s 6
			IL_05bc: ldloc.1
			IL_05bd: ldstr "a press begun on the land, swung up at the sky, keeps its point by the last land"
			IL_05c2: ldsfld bool FirstPerson.GodView::MouseValid
			IL_05c7: brtrue.s IL_05df

			IL_05c9: ldloc.s 6
			IL_05cb: ldarg.0
			IL_05cc: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<lastLand>5__9'
			IL_05d1: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_05d6: ldc.r4 6
			IL_05db: clt
			IL_05dd: br.s IL_05e0

			IL_05df: ldc.i4.0

			IL_05e0: ldstr "on the land {0}, game point {1:F0},{2:F0}, last land {3:F0},{4:F0}"
			IL_05e5: ldc.i4.5
			IL_05e6: newarr [mscorlib]System.Object
			IL_05eb: dup
			IL_05ec: ldc.i4.0
			IL_05ed: ldsfld bool FirstPerson.GodView::MouseValid
			IL_05f2: box [mscorlib]System.Boolean
			IL_05f7: stelem.ref
			IL_05f8: dup
			IL_05f9: ldc.i4.1
			IL_05fa: ldloc.s 6
			IL_05fc: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0601: box [mscorlib]System.Single
			IL_0606: stelem.ref
			IL_0607: dup
			IL_0608: ldc.i4.2
			IL_0609: ldloc.s 6
			IL_060b: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0610: box [mscorlib]System.Single
			IL_0615: stelem.ref
			IL_0616: dup
			IL_0617: ldc.i4.3
			IL_0618: ldarg.0
			IL_0619: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<lastLand>5__9'
			IL_061e: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0623: box [mscorlib]System.Single
			IL_0628: stelem.ref
			IL_0629: dup
			IL_062a: ldc.i4.4
			IL_062b: ldarg.0
			IL_062c: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<lastLand>5__9'
			IL_0631: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0636: box [mscorlib]System.Single
			IL_063b: stelem.ref
			IL_063c: call string [mscorlib]System.String::Format(string, object[])
			IL_0641: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0646: pop
			IL_0647: ldloc.1
			IL_0648: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_064d: ldc.r4 0.0
			IL_0652: stfld float32 FirstPerson.Vr.VrSystem::SimUse
			IL_0657: ldarg.0
			IL_0658: ldc.r4 0.3
			IL_065d: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0662: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0667: ldarg.0
			IL_0668: ldc.i4.s 12
			IL_066a: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_066f: ldc.i4.1
			IL_0670: ret

			IL_0671: ldarg.0
			IL_0672: ldc.i4.m1
			IL_0673: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0678: ldarg.0
			IL_0679: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<start>5__7'
			IL_067e: ldarg.0
			IL_067f: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Sticks>d__15'::'<before>5__8'
			IL_0684: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0689: stloc.s 7
			IL_068b: ldloc.1
			IL_068c: ldstr "a grip held drags the land with the laser"
			IL_0691: ldloc.s 7
			IL_0693: ldc.r4 1
			IL_0698: cgt
			IL_069a: ldstr "{0:F1} tiles"
			IL_069f: ldloc.s 7
			IL_06a1: box [mscorlib]System.Single
			IL_06a6: call string [mscorlib]System.String::Format(string, object)
			IL_06ab: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_06b0: pop
			IL_06b1: ldloc.1
			IL_06b2: ldstr "dragging, the laser swung up at the sky holds the map still"
			IL_06b7: ldarg.0
			IL_06b8: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<flung>5__10'
			IL_06bd: ldc.r4 2
			IL_06c2: clt
			IL_06c4: ldstr "{0:F1} tiles"
			IL_06c9: ldarg.0
			IL_06ca: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<flung>5__10'
			IL_06cf: box [mscorlib]System.Single
			IL_06d4: call string [mscorlib]System.String::Format(string, object)
			IL_06d9: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_06de: pop
			IL_06df: ldloc.1
			IL_06e0: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_06e5: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
			IL_06ea: ldc.i4.1
			IL_06eb: ldc.i4.0
			IL_06ec: stelem.i1
			IL_06ed: ldarg.0
			IL_06ee: ldloc.1
			IL_06ef: ldloc.1
			IL_06f0: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_06f5: ldc.r4 20
			IL_06fa: ldc.r4 0.5
			IL_06ff: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_0704: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0709: ldarg.0
			IL_070a: ldc.i4.s 13
			IL_070c: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0711: ldc.i4.1
			IL_0712: ret

			IL_0713: ldarg.0
			IL_0714: ldc.i4.m1
			IL_0715: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_071a: ldarg.0
			IL_071b: ldsfld class ['Assembly-CSharp']MoveCamera ['Assembly-CSharp']MoveCamera::'instance'
			IL_0720: ldfld float32 ['Assembly-CSharp']MoveCamera::_target_zoom
			IL_0725: stfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<zoom>5__11'
			IL_072a: ldloc.1
			IL_072b: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0730: ldc.r4 0.0
			IL_0735: ldc.r4 1
			IL_073a: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector2::.ctor(float32, float32)
			IL_073f: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimTurn
			IL_0744: ldarg.0
			IL_0745: ldc.r4 0.6
			IL_074a: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_074f: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0754: ldarg.0
			IL_0755: ldc.i4.s 14
			IL_0757: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_075c: ldc.i4.1
			IL_075d: ret

			IL_075e: ldarg.0
			IL_075f: ldc.i4.m1
			IL_0760: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0765: ldloc.1
			IL_0766: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_076b: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Vector2::get_zero()
			IL_0770: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrSystem::SimTurn
			IL_0775: ldloc.1
			IL_0776: ldstr "the right stick pushed up zooms in"
			IL_077b: ldsfld class ['Assembly-CSharp']MoveCamera ['Assembly-CSharp']MoveCamera::'instance'
			IL_0780: ldfld float32 ['Assembly-CSharp']MoveCamera::_target_zoom
			IL_0785: ldarg.0
			IL_0786: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<zoom>5__11'
			IL_078b: ldc.r4 0.8
			IL_0790: mul
			IL_0791: clt
			IL_0793: ldstr "{0:F1} -> {1:F1}"
			IL_0798: ldarg.0
			IL_0799: ldfld float32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<zoom>5__11'
			IL_079e: box [mscorlib]System.Single
			IL_07a3: ldsfld class ['Assembly-CSharp']MoveCamera ['Assembly-CSharp']MoveCamera::'instance'
			IL_07a8: ldfld float32 ['Assembly-CSharp']MoveCamera::_target_zoom
			IL_07ad: box [mscorlib]System.Single
			IL_07b2: call string [mscorlib]System.String::Format(string, object, object)
			IL_07b7: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_07bc: pop
			IL_07bd: ldarg.0
			IL_07be: ldc.r4 0.4
			IL_07c3: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_07c8: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_07cd: ldarg.0
			IL_07ce: ldc.i4.s 15
			IL_07d0: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_07d5: ldc.i4.1
			IL_07d6: ret

			IL_07d7: ldarg.0
			IL_07d8: ldc.i4.m1
			IL_07d9: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_07de: ldarg.0
			IL_07df: ldloc.1
			IL_07e0: ldloc.1
			IL_07e1: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_07e6: ldc.r4 20
			IL_07eb: ldc.r4 0.8
			IL_07f0: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_07f5: stfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_07fa: ldarg.0
			IL_07fb: ldc.i4.s 16
			IL_07fd: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_0802: ldc.i4.1
			IL_0803: ret

			IL_0804: ldarg.0
			IL_0805: ldc.i4.m1
			IL_0806: stfld int32 FirstPerson.VrUiTest/'<Sticks>d__15'::'<>1__state'
			IL_080b: ldc.i4.0
			IL_080c: ret
		} // end of method '<Sticks>d__15'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x51f805
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0006: ret
		} // end of method '<Sticks>d__15'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x51f80d
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<Sticks>d__15'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x51f814
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Sticks>d__15'::'<>2__current'
			IL_0006: ret
		} // end of method '<Sticks>d__15'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<Sticks>d__15'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<Sticks>d__15'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <Sticks>d__15

	.class nested private auto ansi sealed beforefieldinit '<Windows>d__17'
		extends [mscorlib]System.Object
		implements class [mscorlib]System.Collections.Generic.IEnumerator`1<object>,
		           [mscorlib]System.IDisposable,
		           [mscorlib]System.Collections.IEnumerator
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = (
			01 00 00 00
		)
		// Fields
		.field private int32 '<>1__state'
		.field private object '<>2__current'
		.field public class FirstPerson.VrUiTest '<>4__this'
		.field public class ['Assembly-CSharp']Actor king
		.field private class ['Assembly-CSharp']Actor '<me>5__2'
		.field private int32 '<fades>5__3'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<spot>5__4'
		.field private int32 '<takes>5__5'
		.field private bool '<dived>5__6'
		.field private class FirstPerson.SettingsMenu '<menu>5__7'
		.field private valuetype FirstPerson.Vr.Xr/Pose '<placed>5__8'
		.field private float32 '<sw>5__9'
		.field private float32 '<sh>5__10'
		.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 '<tab>5__11'
		.field private int32 '<presses>5__12'
		.field private bool '<held>5__13'
		.field private int32 '<i>5__14'

		// Methods
		.method public hidebysig specialname rtspecialname 
			instance void .ctor (
				int32 '<>1__state'
			) cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			// Method begins at RVA 0x51f81c
			// Header size: 1
			// Code size: 14 (0xe)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: call instance void [mscorlib]System.Object::.ctor()
			IL_0006: ldarg.0
			IL_0007: ldarg.1
			IL_0008: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_000d: ret
		} // end of method '<Windows>d__17'::.ctor

		.method private final hidebysig newslot virtual 
			instance void System.IDisposable.Dispose () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.IDisposable::Dispose()
			// Method begins at RVA 0x51f82b
			// Header size: 1
			// Code size: 1 (0x1)
			.maxstack 8

			IL_0000: ret
		} // end of method '<Windows>d__17'::System.IDisposable.Dispose

		.method private final hidebysig newslot virtual 
			instance bool MoveNext () cil managed 
		{
			.override method instance bool [mscorlib]System.Collections.IEnumerator::MoveNext()
			// Method begins at RVA 0x51f830
			// Header size: 12
			// Code size: 2729 (0xaa9)
			.maxstack 8
			.locals init (
				[0] int32,
				[1] class FirstPerson.VrUiTest,
				[2] class FirstPerson.Vr.VrSystem/Layers,
				[3] bool,
				[4] valuetype FirstPerson.Vr.Xr/Pose,
				[5] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
				[6] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>,
				[7] class ['Assembly-CSharp']Actor,
				[8] valuetype FirstPerson.Vr.Xr/Vec3,
				[9] int32,
				[10] valuetype FirstPerson.Vr.Xr/Vec3,
				[11] float32
			)

			IL_0000: ldarg.0
			IL_0001: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0006: stloc.0
			IL_0007: ldarg.0
			IL_0008: ldfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Windows>d__17'::'<>4__this'
			IL_000d: stloc.1
			IL_000e: ldloc.0
			IL_000f: switch (IL_0062, IL_0168, IL_01d9, IL_0257, IL_02a9, IL_030a, IL_047e, IL_04c7, IL_0645, IL_0741, IL_07c9, IL_0806, IL_08ad, IL_0922, IL_0953, IL_09f2, IL_0a0a, IL_0a37, IL_0aa0)

			IL_0060: ldc.i4.0
			IL_0061: ret

			IL_0062: ldarg.0
			IL_0063: ldc.i4.m1
			IL_0064: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0069: ldarg.0
			IL_006a: ldnull
			IL_006b: stfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
			IL_0070: ldloc.1
			IL_0071: ldfld class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']Actor> FirstPerson.CivilTest::Founded
			IL_0076: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']Actor>::GetEnumerator()
			IL_007b: stloc.s 6
			.try
			{
				IL_007d: br.s IL_00b2
				// loop start (head: IL_00b2)
					IL_007f: ldloca.s 6
					IL_0081: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>::get_Current()
					IL_0086: stloc.s 7
					IL_0088: ldloc.s 7
					IL_008a: brfalse.s IL_00b2

					IL_008c: ldloc.s 7
					IL_008e: callvirt instance bool ['Assembly-CSharp']NanoObject::isAlive()
					IL_0093: brfalse.s IL_00b2

					IL_0095: ldloc.s 7
					IL_0097: ldarg.0
					IL_0098: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::king
					IL_009d: beq.s IL_00b2

					IL_009f: ldloc.s 7
					IL_00a1: callvirt instance bool ['Assembly-CSharp']Actor::isAdult()
					IL_00a6: brfalse.s IL_00b2

					IL_00a8: ldarg.0
					IL_00a9: ldloc.s 7
					IL_00ab: stfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
					IL_00b0: leave.s IL_00cb

					IL_00b2: ldloca.s 6
					IL_00b4: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>::MoveNext()
					IL_00b9: brtrue.s IL_007f
				// end loop

				IL_00bb: leave.s IL_00cb
			} // end .try
			finally
			{
				IL_00bd: ldloca.s 6
				IL_00bf: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>
				IL_00c5: callvirt instance void [mscorlib]System.IDisposable::Dispose()
				IL_00ca: endfinally
			} // end handler

			IL_00cb: ldarg.0
			IL_00cc: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
			IL_00d1: brtrue.s IL_00df

			IL_00d3: ldarg.0
			IL_00d4: ldarg.0
			IL_00d5: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::king
			IL_00da: stfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'

			IL_00df: ldarg.0
			IL_00e0: ldloc.1
			IL_00e1: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_00e6: ldfld int32 FirstPerson.WorldBoxMod::VrFadesForTest
			IL_00eb: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<fades>5__3'
			.try
			{
				IL_00f0: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
				IL_00f5: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::unselectAll()
				IL_00fa: leave.s IL_00ff
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_00fc: pop
				IL_00fd: leave.s IL_00ff
			} // end handler

			IL_00ff: ldloc.1
			IL_0100: ldloca.s 8
			IL_0102: initobj FirstPerson.Vr.Xr/Vec3
			IL_0108: ldloca.s 8
			IL_010a: ldc.r4 0.0
			IL_010f: stfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_0114: ldloca.s 8
			IL_0116: ldc.r4 0.0
			IL_011b: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_0120: ldloca.s 8
			IL_0122: ldc.r4 -1
			IL_0127: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_012c: ldloc.s 8
			IL_012e: ldc.r4 0.15
			IL_0133: ldc.r4 -0.25
			IL_0138: ldc.r4 -0.2
			IL_013d: ldc.i4.1
			IL_013e: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
			IL_0143: ldarg.0
			IL_0144: ldloc.1
			IL_0145: ldloc.1
			IL_0146: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_014b: ldc.r4 20
			IL_0150: ldc.r4 0.5
			IL_0155: call instance class [mscorlib]System.Collections.IEnumerator FirstPerson.VrUiTest::Hold(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32, float32)
			IL_015a: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_015f: ldarg.0
			IL_0160: ldc.i4.1
			IL_0161: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0166: ldc.i4.1
			IL_0167: ret

			IL_0168: ldarg.0
			IL_0169: ldc.i4.m1
			IL_016a: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_016f: call void FirstPerson.GodView::Resolve()
			IL_0174: ldarg.0
			IL_0175: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.GodView::LandPoint
			IL_017a: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<spot>5__4'
			IL_017f: ldarg.0
			IL_0180: ldc.i4.0
			IL_0181: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_0186: br.s IL_01f2

			IL_0188: ldarg.0
			IL_0189: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
			IL_018e: ldarg.0
			IL_018f: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<spot>5__4'
			IL_0194: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']BaseSimObject::current_position
			.try
			{
				IL_0199: ldarg.0
				IL_019a: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
				IL_019f: callvirt instance void ['Assembly-CSharp']Actor::cancelAllBeh()
				IL_01a4: ldarg.0
				IL_01a5: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
				IL_01aa: ldc.r4 2
				IL_01af: callvirt instance void ['Assembly-CSharp']Actor::makeWait(float32)
				IL_01b4: leave.s IL_01b9
			} // end .try
			catch [mscorlib]System.Object
			{
				IL_01b6: pop
				IL_01b7: leave.s IL_01b9
			} // end handler

			IL_01b9: ldloc.1
			IL_01ba: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_01bf: ldc.r4 20
			IL_01c4: call void FirstPerson.VrUiTest::HoldNow(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
			IL_01c9: ldarg.0
			IL_01ca: ldnull
			IL_01cb: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_01d0: ldarg.0
			IL_01d1: ldc.i4.2
			IL_01d2: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_01d7: ldc.i4.1
			IL_01d8: ret

			IL_01d9: ldarg.0
			IL_01da: ldc.i4.m1
			IL_01db: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_01e0: ldarg.0
			IL_01e1: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_01e6: stloc.s 9
			IL_01e8: ldarg.0
			IL_01e9: ldloc.s 9
			IL_01eb: ldc.i4.1
			IL_01ec: add
			IL_01ed: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'

			IL_01f2: ldarg.0
			IL_01f3: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_01f8: ldc.i4.s 20
			IL_01fa: blt.s IL_0188

			IL_01fc: ldarg.0
			IL_01fd: ldloc.1
			IL_01fe: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0203: ldfld int32 FirstPerson.WorldBoxMod::VrTakeOversForTest
			IL_0208: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<takes>5__5'
			IL_020d: ldloc.1
			IL_020e: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0213: ldc.r4 1
			IL_0218: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_021d: ldarg.0
			IL_021e: ldc.i4.0
			IL_021f: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_0224: br.s IL_0270

			IL_0226: ldarg.0
			IL_0227: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
			IL_022c: ldarg.0
			IL_022d: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<spot>5__4'
			IL_0232: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']BaseSimObject::current_position
			IL_0237: ldloc.1
			IL_0238: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest::_home
			IL_023d: ldc.r4 20
			IL_0242: call void FirstPerson.VrUiTest::HoldNow(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, float32)
			IL_0247: ldarg.0
			IL_0248: ldnull
			IL_0249: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_024e: ldarg.0
			IL_024f: ldc.i4.3
			IL_0250: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0255: ldc.i4.1
			IL_0256: ret

			IL_0257: ldarg.0
			IL_0258: ldc.i4.m1
			IL_0259: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_025e: ldarg.0
			IL_025f: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_0264: stloc.s 9
			IL_0266: ldarg.0
			IL_0267: ldloc.s 9
			IL_0269: ldc.i4.1
			IL_026a: add
			IL_026b: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'

			IL_0270: ldarg.0
			IL_0271: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_0276: ldc.i4.4
			IL_0277: blt.s IL_0226

			IL_0279: ldloc.1
			IL_027a: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_027f: ldc.r4 0.0
			IL_0284: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_0289: ldarg.0
			IL_028a: ldc.i4.0
			IL_028b: stfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_0290: ldarg.0
			IL_0291: ldc.i4.0
			IL_0292: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_0297: br.s IL_02da

			IL_0299: ldarg.0
			IL_029a: ldnull
			IL_029b: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_02a0: ldarg.0
			IL_02a1: ldc.i4.4
			IL_02a2: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_02a7: ldc.i4.1
			IL_02a8: ret

			IL_02a9: ldarg.0
			IL_02aa: ldc.i4.m1
			IL_02ab: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_02b0: ldarg.0
			IL_02b1: ldarg.0
			IL_02b2: ldfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_02b7: ldloc.1
			IL_02b8: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02bd: callvirt instance bool FirstPerson.WorldBoxMod::get_Diving()
			IL_02c2: or
			IL_02c3: stfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_02c8: ldarg.0
			IL_02c9: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_02ce: stloc.s 9
			IL_02d0: ldarg.0
			IL_02d1: ldloc.s 9
			IL_02d3: ldc.i4.1
			IL_02d4: add
			IL_02d5: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'

			IL_02da: ldarg.0
			IL_02db: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_02e0: ldc.i4.s 60
			IL_02e2: bge.s IL_02f1

			IL_02e4: ldloc.1
			IL_02e5: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_02ea: callvirt instance bool FirstPerson.WorldBoxMod::get_IsFirstPerson()
			IL_02ef: brfalse.s IL_0299

			IL_02f1: ldarg.0
			IL_02f2: ldc.i4.0
			IL_02f3: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_02f8: br.s IL_033b

			IL_02fa: ldarg.0
			IL_02fb: ldnull
			IL_02fc: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0301: ldarg.0
			IL_0302: ldc.i4.5
			IL_0303: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0308: ldc.i4.1
			IL_0309: ret

			IL_030a: ldarg.0
			IL_030b: ldc.i4.m1
			IL_030c: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0311: ldarg.0
			IL_0312: ldarg.0
			IL_0313: ldfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_0318: ldloc.1
			IL_0319: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_031e: callvirt instance bool FirstPerson.WorldBoxMod::get_Diving()
			IL_0323: or
			IL_0324: stfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_0329: ldarg.0
			IL_032a: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_032f: stloc.s 9
			IL_0331: ldarg.0
			IL_0332: ldloc.s 9
			IL_0334: ldc.i4.1
			IL_0335: add
			IL_0336: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'

			IL_033b: ldarg.0
			IL_033c: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<i>5__14'
			IL_0341: ldc.i4.6
			IL_0342: blt.s IL_02fa

			IL_0344: ldloc.1
			IL_0345: ldstr "the trigger on a creature, no power picked, takes it over"
			IL_034a: ldloc.1
			IL_034b: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0350: ldfld int32 FirstPerson.WorldBoxMod::VrTakeOversForTest
			IL_0355: ldarg.0
			IL_0356: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<takes>5__5'
			IL_035b: cgt
			IL_035d: ldstr "{0} taken over"
			IL_0362: ldloc.1
			IL_0363: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0368: ldfld int32 FirstPerson.WorldBoxMod::VrTakeOversForTest
			IL_036d: ldarg.0
			IL_036e: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<takes>5__5'
			IL_0373: sub
			IL_0374: box [mscorlib]System.Int32
			IL_0379: call string [mscorlib]System.String::Format(string, object)
			IL_037e: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0383: pop
			IL_0384: ldloc.1
			IL_0385: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_038a: callvirt instance bool FirstPerson.WorldBoxMod::get_IsFirstPerson()
			IL_038f: brfalse.s IL_03f3

			IL_0391: ldloc.1
			IL_0392: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0397: callvirt instance class ['Assembly-CSharp']Actor FirstPerson.WorldBoxMod::get_Host()
			IL_039c: brfalse.s IL_03f3

			IL_039e: ldloc.1
			IL_039f: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_03a4: callvirt instance class ['Assembly-CSharp']Actor FirstPerson.WorldBoxMod::get_Host()
			IL_03a9: ldarg.0
			IL_03aa: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
			IL_03af: beq.s IL_03f3

			IL_03b1: ldloc.1
			IL_03b2: ldstr "took over "
			IL_03b7: ldloc.1
			IL_03b8: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_03bd: callvirt instance class ['Assembly-CSharp']Actor FirstPerson.WorldBoxMod::get_Host()
			IL_03c2: ldfld class ['Assembly-CSharp']ActorAsset ['Assembly-CSharp']Actor::asset
			IL_03c7: dup
			IL_03c8: brtrue.s IL_03ce

			IL_03ca: pop
			IL_03cb: ldnull
			IL_03cc: br.s IL_03d3

			IL_03ce: ldfld string ['Assembly-CSharp']Asset::id

			IL_03d3: ldstr " (the one on the laser) rather than the one stood there"
			IL_03d8: call string [mscorlib]System.String::Concat(string, string, string)
			IL_03dd: call instance void FirstPerson.CivilTest::Note(string)
			IL_03e2: ldarg.0
			IL_03e3: ldloc.1
			IL_03e4: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_03e9: callvirt instance class ['Assembly-CSharp']Actor FirstPerson.WorldBoxMod::get_Host()
			IL_03ee: stfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'

			IL_03f3: ldloc.1
			IL_03f4: ldstr "first person came up"
			IL_03f9: ldloc.1
			IL_03fa: ldarg.0
			IL_03fb: ldfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::'<me>5__2'
			IL_0400: call instance bool FirstPerson.CivilTest::InControl(class ['Assembly-CSharp']Actor)
			IL_0405: ldnull
			IL_0406: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_040b: brtrue.s IL_040f

			IL_040d: ldc.i4.0
			IL_040e: ret

			IL_040f: ldloc.1
			IL_0410: ldstr "taking control in the headset doesn't dive: it fades"
			IL_0415: ldarg.0
			IL_0416: ldfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_041b: brtrue.s IL_0432

			IL_041d: ldloc.1
			IL_041e: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0423: ldfld int32 FirstPerson.WorldBoxMod::VrFadesForTest
			IL_0428: ldarg.0
			IL_0429: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<fades>5__3'
			IL_042e: cgt
			IL_0430: br.s IL_0433

			IL_0432: ldc.i4.0

			IL_0433: ldstr "dived {0}, fades {1}"
			IL_0438: ldarg.0
			IL_0439: ldfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<dived>5__6'
			IL_043e: box [mscorlib]System.Boolean
			IL_0443: ldloc.1
			IL_0444: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0449: ldfld int32 FirstPerson.WorldBoxMod::VrFadesForTest
			IL_044e: ldarg.0
			IL_044f: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<fades>5__3'
			IL_0454: sub
			IL_0455: box [mscorlib]System.Int32
			IL_045a: call string [mscorlib]System.String::Format(string, object, object)
			IL_045f: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0464: pop
			IL_0465: ldarg.0
			IL_0466: ldc.r4 1
			IL_046b: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0470: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0475: ldarg.0
			IL_0476: ldc.i4.6
			IL_0477: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_047c: ldc.i4.1
			IL_047d: ret

			IL_047e: ldarg.0
			IL_047f: ldc.i4.m1
			IL_0480: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0485: ldarg.0
			IL_0486: ldloc.1
			IL_0487: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_048c: callvirt instance class FirstPerson.SettingsMenu FirstPerson.WorldBoxMod::get_SettingsMenu()
			IL_0491: stfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_0496: ldarg.0
			IL_0497: ldfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_049c: ldc.i4.0
			IL_049d: stfld int32 FirstPerson.SettingsMenu::Tab
			IL_04a2: ldloc.1
			IL_04a3: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_04a8: ldc.i4.1
			IL_04a9: callvirt instance void FirstPerson.WorldBoxMod::SetSettingsMenu(bool)
			IL_04ae: ldarg.0
			IL_04af: ldc.r4 0.8
			IL_04b4: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_04b9: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_04be: ldarg.0
			IL_04bf: ldc.i4.7
			IL_04c0: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_04c5: ldc.i4.1
			IL_04c6: ret

			IL_04c7: ldarg.0
			IL_04c8: ldc.i4.m1
			IL_04c9: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_04ce: ldloc.1
			IL_04cf: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_04d4: ldfld class FirstPerson.Vr.VrSystem/Layers FirstPerson.Vr.VrSystem::SimLast
			IL_04d9: stloc.2
			IL_04da: ldloc.2
			IL_04db: ldfld int32 FirstPerson.Vr.VrSystem/Layers::QuadCount
			IL_04e0: ldc.i4.0
			IL_04e1: ble.s IL_04f5

			IL_04e3: ldloc.2
			IL_04e4: ldfld class FirstPerson.Vr.VrSystem/Quad[] FirstPerson.Vr.VrSystem/Layers::Quads
			IL_04e9: ldc.i4.0
			IL_04ea: ldelem.ref
			IL_04eb: ldfld bool FirstPerson.Vr.VrSystem/Quad::OnHead
			IL_04f0: ldc.i4.0
			IL_04f1: ceq
			IL_04f3: br.s IL_04f6

			IL_04f5: ldc.i4.0

			IL_04f6: stloc.3
			IL_04f7: ldloc.1
			IL_04f8: ldstr "a window hangs in the room in front of you"
			IL_04fd: ldloc.1
			IL_04fe: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0503: callvirt instance bool FirstPerson.WorldBoxMod::get_VrWindowUpForTest()
			IL_0508: ldloc.3
			IL_0509: and
			IL_050a: ldstr "window {0}, quads {1}"
			IL_050f: ldloc.1
			IL_0510: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0515: callvirt instance bool FirstPerson.WorldBoxMod::get_VrWindowUpForTest()
			IL_051a: box [mscorlib]System.Boolean
			IL_051f: ldloc.2
			IL_0520: ldfld int32 FirstPerson.Vr.VrSystem/Layers::QuadCount
			IL_0525: box [mscorlib]System.Int32
			IL_052a: call string [mscorlib]System.String::Format(string, object, object)
			IL_052f: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0534: pop
			IL_0535: ldloc.3
			IL_0536: brfalse IL_05ed

			IL_053b: ldloc.2
			IL_053c: ldfld class FirstPerson.Vr.VrSystem/Quad[] FirstPerson.Vr.VrSystem/Layers::Quads
			IL_0541: ldc.i4.0
			IL_0542: ldelem.ref
			IL_0543: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.Vr.VrSystem/Quad::Pose
			IL_0548: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_054d: stloc.s 10
			IL_054f: ldc.r4 2
			IL_0554: ldloc.2
			IL_0555: ldfld class FirstPerson.Vr.VrSystem/Quad[] FirstPerson.Vr.VrSystem/Layers::Quads
			IL_055a: ldc.i4.0
			IL_055b: ldelem.ref
			IL_055c: ldfld float32 FirstPerson.Vr.VrSystem/Quad::MetresWide
			IL_0561: ldc.r4 0.5
			IL_0566: mul
			IL_0567: ldloc.s 10
			IL_0569: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_056e: ldloc.s 10
			IL_0570: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_0575: mul
			IL_0576: ldloc.s 10
			IL_0578: ldfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_057d: ldloc.s 10
			IL_057f: ldfld float32 FirstPerson.Vr.Xr/Vec3::Y
			IL_0584: mul
			IL_0585: add
			IL_0586: ldloc.s 10
			IL_0588: ldfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_058d: ldloc.s 10
			IL_058f: ldfld float32 FirstPerson.Vr.Xr/Vec3::Z
			IL_0594: mul
			IL_0595: add
			IL_0596: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sqrt(float32)
			IL_059b: div
			IL_059c: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Atan(float32)
			IL_05a1: mul
			IL_05a2: ldc.r4 57.29578
			IL_05a7: mul
			IL_05a8: stloc.s 11
			IL_05aa: ldloc.1
			IL_05ab: ldstr "...as wide as the view (a Quest 2's: about 94 degrees across)"
			IL_05b0: ldloc.s 11
			IL_05b2: ldc.r4 85
			IL_05b7: ble.un.s IL_05d5

			IL_05b9: ldloc.s 11
			IL_05bb: ldloc.1
			IL_05bc: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_05c1: callvirt instance float32 FirstPerson.WorldBoxMod::get_VrHudDegreesForTest()
			IL_05c6: sub
			IL_05c7: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_05cc: ldc.r4 1
			IL_05d1: clt
			IL_05d3: br.s IL_05d6

			IL_05d5: ldc.i4.0

			IL_05d6: ldstr "{0:F1} deg across"
			IL_05db: ldloc.s 11
			IL_05dd: box [mscorlib]System.Single
			IL_05e2: call string [mscorlib]System.String::Format(string, object)
			IL_05e7: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_05ec: pop

			IL_05ed: ldarg.0
			IL_05ee: ldloc.1
			IL_05ef: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_05f4: callvirt instance valuetype FirstPerson.Vr.Xr/Pose FirstPerson.WorldBoxMod::get_VrAimPanelPoseForTest()
			IL_05f9: stfld valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<Windows>d__17'::'<placed>5__8'
			IL_05fe: ldloc.1
			IL_05ff: ldc.r4 0.0
			IL_0604: ldc.r4 1
			IL_0609: ldc.r4 0.0
			IL_060e: ldc.r4 35
			IL_0613: call valuetype FirstPerson.Vr.Xr/Quat FirstPerson.VrUiTest::Quat(float32, float32, float32, float32)
			IL_0618: ldc.r4 0.0
			IL_061d: ldc.r4 0.0
			IL_0622: ldc.r4 0.0
			IL_0627: call instance void FirstPerson.VrUiTest::Head(valuetype FirstPerson.Vr.Xr/Quat, float32, float32, float32)
			IL_062c: ldarg.0
			IL_062d: ldc.r4 0.4
			IL_0632: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0637: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_063c: ldarg.0
			IL_063d: ldc.i4.8
			IL_063e: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0643: ldc.i4.1
			IL_0644: ret

			IL_0645: ldarg.0
			IL_0646: ldc.i4.m1
			IL_0647: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_064c: ldloc.1
			IL_064d: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0652: callvirt instance valuetype FirstPerson.Vr.Xr/Pose FirstPerson.WorldBoxMod::get_VrAimPanelPoseForTest()
			IL_0657: stloc.s 4
			IL_0659: ldloc.1
			IL_065a: ldstr "turning the head to read its side, the window stays put"
			IL_065f: ldloc.s 4
			IL_0661: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_0666: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_066b: ldarg.0
			IL_066c: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<Windows>d__17'::'<placed>5__8'
			IL_0671: ldflda valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_0676: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_067b: sub
			IL_067c: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
			IL_0681: ldc.r4 0.02
			IL_0686: clt
			IL_0688: ldstr "{0:F2} -> {1:F2} m across"
			IL_068d: ldarg.0
			IL_068e: ldflda valuetype FirstPerson.Vr.Xr/Pose FirstPerson.VrUiTest/'<Windows>d__17'::'<placed>5__8'
			IL_0693: ldflda valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_0698: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_069d: box [mscorlib]System.Single
			IL_06a2: ldloc.s 4
			IL_06a4: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
			IL_06a9: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
			IL_06ae: box [mscorlib]System.Single
			IL_06b3: call string [mscorlib]System.String::Format(string, object, object)
			IL_06b8: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_06bd: pop
			IL_06be: ldarg.0
			IL_06bf: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_width()
			IL_06c4: conv.r4
			IL_06c5: stfld float32 FirstPerson.VrUiTest/'<Windows>d__17'::'<sw>5__9'
			IL_06ca: ldarg.0
			IL_06cb: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_height()
			IL_06d0: conv.r4
			IL_06d1: stfld float32 FirstPerson.VrUiTest/'<Windows>d__17'::'<sh>5__10'
			IL_06d6: ldarg.0
			IL_06d7: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Rect[] FirstPerson.Hud::TabOnScreen
			IL_06dc: ldc.i4.2
			IL_06dd: ldelema [UnityEngine.CoreModule]UnityEngine.Rect
			IL_06e2: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Rect::get_center()
			IL_06e7: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<tab>5__11'
			IL_06ec: ldloc.1
			IL_06ed: ldstr "the window takes the laser"
			IL_06f2: ldloc.1
			IL_06f3: ldarg.0
			IL_06f4: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<tab>5__11'
			IL_06f9: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_06fe: ldarg.0
			IL_06ff: ldfld float32 FirstPerson.VrUiTest/'<Windows>d__17'::'<sw>5__9'
			IL_0704: div
			IL_0705: ldarg.0
			IL_0706: ldflda valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<tab>5__11'
			IL_070b: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0710: ldarg.0
			IL_0711: ldfld float32 FirstPerson.VrUiTest/'<Windows>d__17'::'<sh>5__10'
			IL_0716: div
			IL_0717: ldc.i4.1
			IL_0718: call instance bool FirstPerson.VrUiTest::PointAtPanel(float32, float32, int32)
			IL_071d: ldnull
			IL_071e: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0723: brtrue.s IL_0727

			IL_0725: ldc.i4.0
			IL_0726: ret

			IL_0727: ldarg.0
			IL_0728: ldc.r4 0.3
			IL_072d: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0732: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0737: ldarg.0
			IL_0738: ldc.i4.s 9
			IL_073a: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_073f: ldc.i4.1
			IL_0740: ret

			IL_0741: ldarg.0
			IL_0742: ldc.i4.m1
			IL_0743: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0748: ldloc.1
			IL_0749: ldstr "the laser is on the window's tab"
			IL_074e: call bool FirstPerson.Vr.VrMouse::get_Driving()
			IL_0753: brfalse.s IL_076e

			IL_0755: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrMouse::get_GuiPoint()
			IL_075a: ldarg.0
			IL_075b: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<tab>5__11'
			IL_0760: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
			IL_0765: ldc.r4 3
			IL_076a: clt
			IL_076c: br.s IL_076f

			IL_076e: ldc.i4.0

			IL_076f: ldstr "at {0}, tab {1}"
			IL_0774: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.Vr.VrMouse::get_GuiPoint()
			IL_0779: box [UnityEngine.CoreModule]UnityEngine.Vector2
			IL_077e: ldarg.0
			IL_077f: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Windows>d__17'::'<tab>5__11'
			IL_0784: box [UnityEngine.CoreModule]UnityEngine.Vector2
			IL_0789: call string [mscorlib]System.String::Format(string, object, object)
			IL_078e: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0793: pop
			IL_0794: ldarg.0
			IL_0795: ldsfld int32 FirstPerson.Vr.VrMouse::GuiPresses
			IL_079a: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<presses>5__12'
			IL_079f: ldloc.1
			IL_07a0: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_07a5: ldc.r4 1
			IL_07aa: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_07af: ldarg.0
			IL_07b0: ldc.r4 0.15
			IL_07b5: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_07ba: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_07bf: ldarg.0
			IL_07c0: ldc.i4.s 10
			IL_07c2: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_07c7: ldc.i4.1
			IL_07c8: ret

			IL_07c9: ldarg.0
			IL_07ca: ldc.i4.m1
			IL_07cb: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_07d0: ldarg.0
			IL_07d1: ldc.i4.0
			IL_07d2: call bool [UnityEngine.InputLegacyModule]UnityEngine.Input::GetMouseButton(int32)
			IL_07d7: stfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<held>5__13'
			IL_07dc: ldloc.1
			IL_07dd: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_07e2: ldc.r4 0.0
			IL_07e7: stfld float32 FirstPerson.Vr.VrSystem::SimAttack
			IL_07ec: ldarg.0
			IL_07ed: ldc.r4 0.3
			IL_07f2: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_07f7: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_07fc: ldarg.0
			IL_07fd: ldc.i4.s 11
			IL_07ff: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0804: ldc.i4.1
			IL_0805: ret

			IL_0806: ldarg.0
			IL_0807: ldc.i4.m1
			IL_0808: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_080d: ldloc.1
			IL_080e: ldstr "the trigger clicked the window (its third tab open)"
			IL_0813: ldarg.0
			IL_0814: ldfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_0819: ldfld int32 FirstPerson.SettingsMenu::Tab
			IL_081e: ldc.i4.2
			IL_081f: ceq
			IL_0821: ldstr "tab {0}, button held {1}, presses played {2}"
			IL_0826: ldarg.0
			IL_0827: ldfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_082c: ldfld int32 FirstPerson.SettingsMenu::Tab
			IL_0831: box [mscorlib]System.Int32
			IL_0836: ldarg.0
			IL_0837: ldfld bool FirstPerson.VrUiTest/'<Windows>d__17'::'<held>5__13'
			IL_083c: box [mscorlib]System.Boolean
			IL_0841: ldsfld int32 FirstPerson.Vr.VrMouse::GuiPresses
			IL_0846: ldarg.0
			IL_0847: ldfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<presses>5__12'
			IL_084c: sub
			IL_084d: box [mscorlib]System.Int32
			IL_0852: call string [mscorlib]System.String::Format(string, object, object, object)
			IL_0857: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_085c: pop
			IL_085d: ldsfld valuetype [UnityEngine.CoreModule]UnityEngine.Rect[] FirstPerson.Hud::TabOnScreen
			IL_0862: ldc.i4.1
			IL_0863: ldelema [UnityEngine.CoreModule]UnityEngine.Rect
			IL_0868: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 [UnityEngine.CoreModule]UnityEngine.Rect::get_center()
			IL_086d: stloc.s 5
			IL_086f: ldloc.1
			IL_0870: ldloc.s 5
			IL_0872: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0877: ldarg.0
			IL_0878: ldfld float32 FirstPerson.VrUiTest/'<Windows>d__17'::'<sw>5__9'
			IL_087d: div
			IL_087e: ldloc.s 5
			IL_0880: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_0885: ldarg.0
			IL_0886: ldfld float32 FirstPerson.VrUiTest/'<Windows>d__17'::'<sh>5__10'
			IL_088b: div
			IL_088c: ldc.i4.0
			IL_088d: call instance bool FirstPerson.VrUiTest::PointAtPanel(float32, float32, int32)
			IL_0892: pop
			IL_0893: ldarg.0
			IL_0894: ldc.r4 0.3
			IL_0899: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_089e: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_08a3: ldarg.0
			IL_08a4: ldc.i4.s 12
			IL_08a6: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_08ab: ldc.i4.1
			IL_08ac: ret

			IL_08ad: ldarg.0
			IL_08ae: ldc.i4.m1
			IL_08af: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_08b4: ldloc.1
			IL_08b5: ldstr "the right hand, not the cursor now, shows its pointer on the window"
			IL_08ba: ldloc.1
			IL_08bb: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_08c0: callvirt instance bool FirstPerson.WorldBoxMod::get_VrOtherOnPanelForTest()
			IL_08c5: brtrue.s IL_08d7

			IL_08c7: ldloc.1
			IL_08c8: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_08cd: callvirt instance int32 FirstPerson.WorldBoxMod::get_VrActiveHandForTest()
			IL_08d2: ldc.i4.0
			IL_08d3: ceq
			IL_08d5: br.s IL_08d8

			IL_08d7: ldc.i4.1

			IL_08d8: ldstr "cursor hand {0}"
			IL_08dd: ldloc.1
			IL_08de: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_08e3: callvirt instance int32 FirstPerson.WorldBoxMod::get_VrActiveHandForTest()
			IL_08e8: box [mscorlib]System.Int32
			IL_08ed: call string [mscorlib]System.String::Format(string, object)
			IL_08f2: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_08f7: pop
			IL_08f8: ldloc.1
			IL_08f9: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_08fe: ldc.r4 1
			IL_0903: stfld float32 FirstPerson.Vr.VrSystem::SimKick
			IL_0908: ldarg.0
			IL_0909: ldc.r4 0.15
			IL_090e: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0913: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0918: ldarg.0
			IL_0919: ldc.i4.s 13
			IL_091b: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0920: ldc.i4.1
			IL_0921: ret

			IL_0922: ldarg.0
			IL_0923: ldc.i4.m1
			IL_0924: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0929: ldloc.1
			IL_092a: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_092f: ldc.r4 0.0
			IL_0934: stfld float32 FirstPerson.Vr.VrSystem::SimKick
			IL_0939: ldarg.0
			IL_093a: ldc.r4 0.3
			IL_093f: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0944: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0949: ldarg.0
			IL_094a: ldc.i4.s 14
			IL_094c: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0951: ldc.i4.1
			IL_0952: ret

			IL_0953: ldarg.0
			IL_0954: ldc.i4.m1
			IL_0955: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_095a: ldloc.1
			IL_095b: ldstr "the left hand's trigger clicks where it points (the second tab open)"
			IL_0960: ldarg.0
			IL_0961: ldfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_0966: ldfld int32 FirstPerson.SettingsMenu::Tab
			IL_096b: ldc.i4.1
			IL_096c: bne.un.s IL_097e

			IL_096e: ldloc.1
			IL_096f: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0974: callvirt instance int32 FirstPerson.WorldBoxMod::get_VrActiveHandForTest()
			IL_0979: ldc.i4.0
			IL_097a: ceq
			IL_097c: br.s IL_097f

			IL_097e: ldc.i4.0

			IL_097f: ldstr "tab {0}, cursor hand {1}"
			IL_0984: ldarg.0
			IL_0985: ldfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_098a: ldfld int32 FirstPerson.SettingsMenu::Tab
			IL_098f: box [mscorlib]System.Int32
			IL_0994: ldloc.1
			IL_0995: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_099a: callvirt instance int32 FirstPerson.WorldBoxMod::get_VrActiveHandForTest()
			IL_099f: box [mscorlib]System.Int32
			IL_09a4: call string [mscorlib]System.String::Format(string, object, object)
			IL_09a9: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_09ae: pop
			IL_09af: ldloc.1
			IL_09b0: ldstr "...and the right hand shows a dimmer pointer where it points"
			IL_09b5: ldloc.1
			IL_09b6: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_09bb: callvirt instance bool FirstPerson.WorldBoxMod::get_VrOtherOnPanelForTest()
			IL_09c0: ldnull
			IL_09c1: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_09c6: pop
			IL_09c7: ldloc.1
			IL_09c8: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_09cd: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
			IL_09d2: ldc.i4.0
			IL_09d3: ldc.i4.0
			IL_09d4: stelem.i1
			IL_09d5: ldloc.1
			IL_09d6: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_09db: ldc.i4.1
			IL_09dc: stfld bool FirstPerson.Vr.VrSystem::SimAbility
			IL_09e1: ldarg.0
			IL_09e2: ldnull
			IL_09e3: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_09e8: ldarg.0
			IL_09e9: ldc.i4.s 15
			IL_09eb: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_09f0: ldc.i4.1
			IL_09f1: ret

			IL_09f2: ldarg.0
			IL_09f3: ldc.i4.m1
			IL_09f4: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_09f9: ldarg.0
			IL_09fa: ldnull
			IL_09fb: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0a00: ldarg.0
			IL_0a01: ldc.i4.s 16
			IL_0a03: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0a08: ldc.i4.1
			IL_0a09: ret

			IL_0a0a: ldarg.0
			IL_0a0b: ldc.i4.m1
			IL_0a0c: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0a11: ldloc.1
			IL_0a12: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0a17: ldc.i4.0
			IL_0a18: stfld bool FirstPerson.Vr.VrSystem::SimAbility
			IL_0a1d: ldarg.0
			IL_0a1e: ldc.r4 0.5
			IL_0a23: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0a28: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0a2d: ldarg.0
			IL_0a2e: ldc.i4.s 17
			IL_0a30: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0a35: ldc.i4.1
			IL_0a36: ret

			IL_0a37: ldarg.0
			IL_0a38: ldc.i4.m1
			IL_0a39: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0a3e: ldloc.1
			IL_0a3f: ldstr "B closes the window"
			IL_0a44: ldarg.0
			IL_0a45: ldfld class FirstPerson.SettingsMenu FirstPerson.VrUiTest/'<Windows>d__17'::'<menu>5__7'
			IL_0a4a: ldfld bool FirstPerson.SettingsMenu::Open
			IL_0a4f: ldc.i4.0
			IL_0a50: ceq
			IL_0a52: ldnull
			IL_0a53: call instance bool FirstPerson.CivilTest::Check(string, bool, string)
			IL_0a58: pop
			IL_0a59: ldloc.1
			IL_0a5a: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0a5f: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
			IL_0a64: ldc.i4.1
			IL_0a65: ldc.i4.0
			IL_0a66: stelem.i1
			IL_0a67: ldloc.1
			IL_0a68: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
			IL_0a6d: callvirt instance class FirstPerson.Hud FirstPerson.WorldBoxMod::get_HudForTest()
			IL_0a72: call string FirstPerson.Log::get_Folder()
			IL_0a77: ldstr "vrui_fp_hud.png"
			IL_0a7c: call string [mscorlib]System.IO.Path::Combine(string, string)
			IL_0a81: callvirt instance void FirstPerson.Hud::CaptureHudForTest(string)
			IL_0a86: ldarg.0
			IL_0a87: ldc.r4 0.3
			IL_0a8c: call class [UnityEngine.CoreModule]UnityEngine.WaitForSecondsRealtime FirstPerson.CivilTest::Wait(float32)
			IL_0a91: stfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0a96: ldarg.0
			IL_0a97: ldc.i4.s 18
			IL_0a99: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0a9e: ldc.i4.1
			IL_0a9f: ret

			IL_0aa0: ldarg.0
			IL_0aa1: ldc.i4.m1
			IL_0aa2: stfld int32 FirstPerson.VrUiTest/'<Windows>d__17'::'<>1__state'
			IL_0aa7: ldc.i4.0
			IL_0aa8: ret
		} // end of method '<Windows>d__17'::MoveNext

		.method private final hidebysig specialname newslot virtual 
			instance object 'System.Collections.Generic.IEnumerator<System.Object>.get_Current' () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance !0 class [mscorlib]System.Collections.Generic.IEnumerator`1<object>::get_Current()
			// Method begins at RVA 0x520310
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0006: ret
		} // end of method '<Windows>d__17'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'

		.method private final hidebysig newslot virtual 
			instance void System.Collections.IEnumerator.Reset () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance void [mscorlib]System.Collections.IEnumerator::Reset()
			// Method begins at RVA 0x520318
			// Header size: 1
			// Code size: 6 (0x6)
			.maxstack 8

			IL_0000: newobj instance void [mscorlib]System.NotSupportedException::.ctor()
			IL_0005: throw
		} // end of method '<Windows>d__17'::System.Collections.IEnumerator.Reset

		.method private final hidebysig specialname newslot virtual 
			instance object System.Collections.IEnumerator.get_Current () cil managed 
		{
			.custom instance void [mscorlib]System.Diagnostics.DebuggerHiddenAttribute::.ctor() = (
				01 00 00 00
			)
			.override method instance object [mscorlib]System.Collections.IEnumerator::get_Current()
			// Method begins at RVA 0x52031f
			// Header size: 1
			// Code size: 7 (0x7)
			.maxstack 8

			IL_0000: ldarg.0
			IL_0001: ldfld object FirstPerson.VrUiTest/'<Windows>d__17'::'<>2__current'
			IL_0006: ret
		} // end of method '<Windows>d__17'::System.Collections.IEnumerator.get_Current

		// Properties
		.property instance object 'System.Collections.Generic.IEnumerator<System.Object>.Current'()
		{
			.get instance object FirstPerson.VrUiTest/'<Windows>d__17'::'System.Collections.Generic.IEnumerator<System.Object>.get_Current'()
		}
		.property instance object System.Collections.IEnumerator.Current()
		{
			.get instance object FirstPerson.VrUiTest/'<Windows>d__17'::System.Collections.IEnumerator.get_Current()
		}

	} // end of class <Windows>d__17


	// Fields
	.field private initonly class [UnityEngine.CoreModule]UnityEngine.RenderTexture[] _images
	.field private valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 _home

	// Methods
	.method family hidebysig specialname virtual 
		instance string get_Prefix () cil managed 
	{
		// Method begins at RVA 0x1ed391
		// Header size: 1
		// Code size: 6 (0x6)
		.maxstack 8

		IL_0000: ldstr "VRUI"
		IL_0005: ret
	} // end of method VrUiTest::get_Prefix

	.method private hidebysig specialname 
		instance class FirstPerson.Vr.VrSystem get_Vr () cil managed 
	{
		// Method begins at RVA 0x1ed398
		// Header size: 1
		// Code size: 12 (0xc)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
		IL_0006: callvirt instance class FirstPerson.Vr.VrSystem FirstPerson.WorldBoxMod::get_VrForTest()
		IL_000b: ret
	} // end of method VrUiTest::get_Vr

	.method family hidebysig virtual 
		instance class [mscorlib]System.Collections.IEnumerator Body () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 1f 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 42 6f 64 79 3e 64 5f
			5f 36 00 00
		)
		// Method begins at RVA 0x1ed3a5
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<Body>d__6'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Body>d__6'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::Body

	.method private hidebysig 
		instance void MakeImages () cil managed 
	{
		// Method begins at RVA 0x1ed3b4
		// Header size: 12
		// Code size: 152 (0x98)
		.maxstack 6
		.locals init (
			[0] int32,
			[1] int32,
			[2] int32
		)

		IL_0000: ldc.i4.0
		IL_0001: stloc.0
		IL_0002: br IL_0090
		// loop start (head: IL_0090)
			IL_0007: ldloc.0
			IL_0008: ldc.i4.2
			IL_0009: blt.s IL_0019

			IL_000b: ldc.i4.s 64
			IL_000d: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_width()
			IL_0012: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::Max(int32, int32)
			IL_0017: br.s IL_001e

			IL_0019: ldc.i4 2048

			IL_001e: stloc.1
			IL_001f: ldloc.0
			IL_0020: ldc.i4.2
			IL_0021: blt.s IL_0031

			IL_0023: ldc.i4.s 64
			IL_0025: call int32 [UnityEngine.CoreModule]UnityEngine.Screen::get_height()
			IL_002a: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::Max(int32, int32)
			IL_002f: br.s IL_0036

			IL_0031: ldc.i4 2048

			IL_0036: stloc.2
			IL_0037: ldarg.0
			IL_0038: ldfld class [UnityEngine.CoreModule]UnityEngine.RenderTexture[] FirstPerson.VrUiTest::_images
			IL_003d: ldloc.0
			IL_003e: ldloc.1
			IL_003f: ldloc.2
			IL_0040: ldc.i4.0
			IL_0041: ldarg.0
			IL_0042: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0047: callvirt instance bool FirstPerson.Vr.VrSystem::get_Bgra()
			IL_004c: brtrue.s IL_0051

			IL_004e: ldc.i4.0
			IL_004f: br.s IL_0053

			IL_0051: ldc.i4.s 20

			IL_0053: newobj instance void [UnityEngine.CoreModule]UnityEngine.RenderTexture::.ctor(int32, int32, int32, valuetype [UnityEngine.CoreModule]UnityEngine.RenderTextureFormat)
			IL_0058: dup
			IL_0059: ldstr "VrUiTestImage"
			IL_005e: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Object::set_name(string)
			IL_0063: stelem.ref
			IL_0064: ldarg.0
			IL_0065: ldfld class [UnityEngine.CoreModule]UnityEngine.RenderTexture[] FirstPerson.VrUiTest::_images
			IL_006a: ldloc.0
			IL_006b: ldelem.ref
			IL_006c: callvirt instance bool [UnityEngine.CoreModule]UnityEngine.RenderTexture::Create()
			IL_0071: pop
			IL_0072: ldarg.0
			IL_0073: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
			IL_0078: ldfld native int[] FirstPerson.Vr.VrSystem::SimImages
			IL_007d: ldloc.0
			IL_007e: ldarg.0
			IL_007f: ldfld class [UnityEngine.CoreModule]UnityEngine.RenderTexture[] FirstPerson.VrUiTest::_images
			IL_0084: ldloc.0
			IL_0085: ldelem.ref
			IL_0086: callvirt instance native int [UnityEngine.CoreModule]UnityEngine.Texture::GetNativeTexturePtr()
			IL_008b: stelem.i
			IL_008c: ldloc.0
			IL_008d: ldc.i4.1
			IL_008e: add
			IL_008f: stloc.0

			IL_0090: ldloc.0
			IL_0091: ldc.i4.3
			IL_0092: blt IL_0007
		// end loop

		IL_0097: ret
	} // end of method VrUiTest::MakeImages

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator GodEyes () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 22 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 47 6f 64 45 79 65 73
			3e 64 5f 5f 38 00 00
		)
		// Method begins at RVA 0x1ed458
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<GodEyes>d__8'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<GodEyes>d__8'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::GodEyes

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator Looks () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 20 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 4c 6f 6f 6b 73 3e 64
			5f 5f 39 00 00
		)
		// Method begins at RVA 0x1ed467
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<Looks>d__9'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Looks>d__9'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::Looks

	.method private hidebysig 
		instance void Point (
			valuetype FirstPerson.Vr.Xr/Vec3 along,
			[opt] float32 x,
			[opt] float32 y,
			[opt] float32 z,
			[opt] int32 hand
		) cil managed 
	{
		.param [2] = float32(0.15)
		.param [3] = float32(-0.25)
		.param [4] = float32(-0.2)
		.param [5] = int32(1)
		// Method begins at RVA 0x1ed478
		// Header size: 12
		// Code size: 213 (0xd5)
		.maxstack 5
		.locals init (
			[0] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion,
			[2] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
			[3] valuetype FirstPerson.Vr.Xr/Pose,
			[4] valuetype FirstPerson.Vr.Xr/Quat,
			[5] valuetype FirstPerson.Vr.Xr/Vec3
		)

		IL_0000: ldarg.1
		IL_0001: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
		IL_0006: ldarg.1
		IL_0007: ldfld float32 FirstPerson.Vr.Xr/Vec3::Y
		IL_000c: ldarg.1
		IL_000d: ldfld float32 FirstPerson.Vr.Xr/Vec3::Z
		IL_0012: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_0017: stloc.2
		IL_0018: ldloca.s 2
		IL_001a: call instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::get_normalized()
		IL_001f: stloc.0
		IL_0020: ldc.r4 0.0
		IL_0025: ldc.r4 0.0
		IL_002a: ldc.r4 -1
		IL_002f: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_0034: ldloc.0
		IL_0035: call valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion [UnityEngine.CoreModule]UnityEngine.Quaternion::FromToRotation(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_003a: stloc.1
		IL_003b: ldarg.0
		IL_003c: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
		IL_0041: ldfld valuetype FirstPerson.Vr.Xr/Pose[] FirstPerson.Vr.VrSystem::SimHands
		IL_0046: ldarg.s hand
		IL_0048: ldloca.s 3
		IL_004a: initobj FirstPerson.Vr.Xr/Pose
		IL_0050: ldloca.s 3
		IL_0052: ldloca.s 4
		IL_0054: initobj FirstPerson.Vr.Xr/Quat
		IL_005a: ldloca.s 4
		IL_005c: ldloc.1
		IL_005d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::x
		IL_0062: stfld float32 FirstPerson.Vr.Xr/Quat::X
		IL_0067: ldloca.s 4
		IL_0069: ldloc.1
		IL_006a: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::y
		IL_006f: stfld float32 FirstPerson.Vr.Xr/Quat::Y
		IL_0074: ldloca.s 4
		IL_0076: ldloc.1
		IL_0077: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::z
		IL_007c: stfld float32 FirstPerson.Vr.Xr/Quat::Z
		IL_0081: ldloca.s 4
		IL_0083: ldloc.1
		IL_0084: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Quaternion::w
		IL_0089: stfld float32 FirstPerson.Vr.Xr/Quat::W
		IL_008e: ldloc.s 4
		IL_0090: stfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
		IL_0095: ldloca.s 3
		IL_0097: ldloca.s 5
		IL_0099: initobj FirstPerson.Vr.Xr/Vec3
		IL_009f: ldloca.s 5
		IL_00a1: ldarg.2
		IL_00a2: stfld float32 FirstPerson.Vr.Xr/Vec3::X
		IL_00a7: ldloca.s 5
		IL_00a9: ldarg.3
		IL_00aa: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
		IL_00af: ldloca.s 5
		IL_00b1: ldarg.s z
		IL_00b3: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
		IL_00b8: ldloc.s 5
		IL_00ba: stfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
		IL_00bf: ldloc.3
		IL_00c0: stelem FirstPerson.Vr.Xr/Pose
		IL_00c5: ldarg.0
		IL_00c6: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
		IL_00cb: ldfld bool[] FirstPerson.Vr.VrSystem::SimHandValid
		IL_00d0: ldarg.s hand
		IL_00d2: ldc.i4.1
		IL_00d3: stelem.i1
		IL_00d4: ret
	} // end of method VrUiTest::Point

	.method private hidebysig 
		instance bool PointAtPanel (
			float32 u,
			float32 v,
			[opt] int32 hand
		) cil managed 
	{
		.param [3] = int32(1)
		// Method begins at RVA 0x1ed55c
		// Header size: 12
		// Code size: 314 (0x13a)
		.maxstack 6
		.locals init (
			[0] valuetype FirstPerson.Vr.Xr/Pose,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Vector2,
			[2] valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion,
			[3] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
			[4] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
			[5] valuetype FirstPerson.Vr.Xr/Vec3
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
		IL_0006: callvirt instance bool FirstPerson.WorldBoxMod::get_VrAimPanelUpForTest()
		IL_000b: brtrue.s IL_000f

		IL_000d: ldc.i4.0
		IL_000e: ret

		IL_000f: ldarg.0
		IL_0010: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
		IL_0015: callvirt instance valuetype FirstPerson.Vr.Xr/Pose FirstPerson.WorldBoxMod::get_VrAimPanelPoseForTest()
		IL_001a: stloc.0
		IL_001b: ldarg.0
		IL_001c: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
		IL_0021: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.WorldBoxMod::get_VrAimPanelSizeForTest()
		IL_0026: stloc.1
		IL_0027: ldloca.s 2
		IL_0029: ldloc.0
		IL_002a: ldfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
		IL_002f: ldfld float32 FirstPerson.Vr.Xr/Quat::X
		IL_0034: ldloc.0
		IL_0035: ldfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
		IL_003a: ldfld float32 FirstPerson.Vr.Xr/Quat::Y
		IL_003f: ldloc.0
		IL_0040: ldfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
		IL_0045: ldfld float32 FirstPerson.Vr.Xr/Quat::Z
		IL_004a: ldloc.0
		IL_004b: ldfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
		IL_0050: ldfld float32 FirstPerson.Vr.Xr/Quat::W
		IL_0055: call instance void [UnityEngine.CoreModule]UnityEngine.Quaternion::.ctor(float32, float32, float32, float32)
		IL_005a: ldloc.0
		IL_005b: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
		IL_0060: ldfld float32 FirstPerson.Vr.Xr/Vec3::X
		IL_0065: ldloc.0
		IL_0066: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
		IL_006b: ldfld float32 FirstPerson.Vr.Xr/Vec3::Y
		IL_0070: ldloc.0
		IL_0071: ldfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
		IL_0076: ldfld float32 FirstPerson.Vr.Xr/Vec3::Z
		IL_007b: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_0080: ldloc.2
		IL_0081: ldarg.1
		IL_0082: ldc.r4 0.5
		IL_0087: sub
		IL_0088: ldloc.1
		IL_0089: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
		IL_008e: mul
		IL_008f: ldc.r4 0.5
		IL_0094: ldarg.2
		IL_0095: sub
		IL_0096: ldloc.1
		IL_0097: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
		IL_009c: mul
		IL_009d: ldc.r4 0.0
		IL_00a2: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_00a7: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Quaternion::op_Multiply(valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_00ac: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Addition(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
		IL_00b1: stloc.3
		IL_00b2: ldloca.s 4
		IL_00b4: ldarg.3
		IL_00b5: ldc.i4.1
		IL_00b6: beq.s IL_00bf

		IL_00b8: ldc.r4 -0.12
		IL_00bd: br.s IL_00c4

		IL_00bf: ldc.r4 0.12

		IL_00c4: ldc.r4 -0.2
		IL_00c9: ldc.r4 -0.1
		IL_00ce: call instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
		IL_00d3: ldarg.0
		IL_00d4: ldloca.s 5
		IL_00d6: initobj FirstPerson.Vr.Xr/Vec3
		IL_00dc: ldloca.s 5
		IL_00de: ldloc.3
		IL_00df: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
		IL_00e4: ldloc.s 4
		IL_00e6: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
		IL_00eb: sub
		IL_00ec: stfld float32 FirstPerson.Vr.Xr/Vec3::X
		IL_00f1: ldloca.s 5
		IL_00f3: ldloc.3
		IL_00f4: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
		IL_00f9: ldloc.s 4
		IL_00fb: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
		IL_0100: sub
		IL_0101: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
		IL_0106: ldloca.s 5
		IL_0108: ldloc.3
		IL_0109: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
		IL_010e: ldloc.s 4
		IL_0110: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
		IL_0115: sub
		IL_0116: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
		IL_011b: ldloc.s 5
		IL_011d: ldloc.s 4
		IL_011f: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
		IL_0124: ldloc.s 4
		IL_0126: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
		IL_012b: ldloc.s 4
		IL_012d: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
		IL_0132: ldarg.3
		IL_0133: call instance void FirstPerson.VrUiTest::Point(valuetype FirstPerson.Vr.Xr/Vec3, float32, float32, float32, int32)
		IL_0138: ldc.i4.1
		IL_0139: ret
	} // end of method VrUiTest::PointAtPanel

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator LaserLand () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 25 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 4c 61 73 65 72 4c 61
			6e 64 3e 64 5f 5f 31 32 00 00
		)
		// Method begins at RVA 0x1ed6a2
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<LaserLand>d__12'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<LaserLand>d__12'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::LaserLand

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator LaserPower () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 26 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 4c 61 73 65 72 50 6f
			77 65 72 3e 64 5f 5f 31 33 00 00
		)
		// Method begins at RVA 0x1ed6b1
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<LaserPower>d__13'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<LaserPower>d__13'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::LaserPower

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator LaserButton () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 27 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 4c 61 73 65 72 42 75
			74 74 6f 6e 3e 64 5f 5f 31 34 00 00
		)
		// Method begins at RVA 0x1ed6c0
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<LaserButton>d__14'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<LaserButton>d__14'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::LaserButton

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator Sticks () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 22 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 53 74 69 63 6b 73 3e
			64 5f 5f 31 35 00 00
		)
		// Method begins at RVA 0x1ed6cf
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<Sticks>d__15'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Sticks>d__15'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::Sticks

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator FlatAndBack () cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 27 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 46 6c 61 74 41 6e 64
			42 61 63 6b 3e 64 5f 5f 31 36 00 00
		)
		// Method begins at RVA 0x1ed6de
		// Header size: 1
		// Code size: 14 (0xe)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<FlatAndBack>d__16'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<FlatAndBack>d__16'::'<>4__this'
		IL_000d: ret
	} // end of method VrUiTest::FlatAndBack

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator Windows (
			class ['Assembly-CSharp']Actor king
		) cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 23 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 57 69 6e 64 6f 77 73
			3e 64 5f 5f 31 37 00 00
		)
		// Method begins at RVA 0x1ed6ed
		// Header size: 1
		// Code size: 21 (0x15)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<Windows>d__17'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.0
		IL_0008: stfld class FirstPerson.VrUiTest FirstPerson.VrUiTest/'<Windows>d__17'::'<>4__this'
		IL_000d: dup
		IL_000e: ldarg.1
		IL_000f: stfld class ['Assembly-CSharp']Actor FirstPerson.VrUiTest/'<Windows>d__17'::king
		IL_0014: ret
	} // end of method VrUiTest::Windows

	.method private hidebysig 
		instance int32 Sheep (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 'at'
		) cil managed 
	{
		// Method begins at RVA 0x1ed704
		// Header size: 12
		// Code size: 125 (0x7d)
		.maxstack 2
		.locals init (
			[0] int32,
			[1] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>,
			[2] class ['Assembly-CSharp']Actor
		)

		IL_0000: ldc.i4.0
		IL_0001: stloc.0
		IL_0002: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
		IL_0007: ldfld class ['Assembly-CSharp']ActorManager ['Assembly-CSharp']MapBox::units
		IL_000c: callvirt instance class [mscorlib]System.Collections.Generic.List`1<!0> class ['Assembly-CSharp']SimSystemManager`2<class ['Assembly-CSharp']Actor, class ['Assembly-CSharp']ActorData>::getSimpleList()
		IL_0011: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<class ['Assembly-CSharp']Actor>::GetEnumerator()
		IL_0016: stloc.1
		.try
		{
			IL_0017: br.s IL_0062
			// loop start (head: IL_0062)
				IL_0019: ldloca.s 1
				IL_001b: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>::get_Current()
				IL_0020: stloc.2
				IL_0021: ldloc.2
				IL_0022: brfalse.s IL_0062

				IL_0024: ldloc.2
				IL_0025: callvirt instance bool ['Assembly-CSharp']NanoObject::isAlive()
				IL_002a: brfalse.s IL_0062

				IL_002c: ldloc.2
				IL_002d: ldfld class ['Assembly-CSharp']ActorAsset ['Assembly-CSharp']Actor::asset
				IL_0032: brfalse.s IL_0062

				IL_0034: ldloc.2
				IL_0035: ldfld class ['Assembly-CSharp']ActorAsset ['Assembly-CSharp']Actor::asset
				IL_003a: ldfld string ['Assembly-CSharp']Asset::id
				IL_003f: ldstr "sheep"
				IL_0044: call bool [mscorlib]System.String::op_Equality(string, string)
				IL_0049: brfalse.s IL_0062

				IL_004b: ldloc.2
				IL_004c: ldfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 ['Assembly-CSharp']BaseSimObject::current_position
				IL_0051: ldarg.1
				IL_0052: call float32 [UnityEngine.CoreModule]UnityEngine.Vector2::Distance(valuetype [UnityEngine.CoreModule]UnityEngine.Vector2, valuetype [UnityEngine.CoreModule]UnityEngine.Vector2)
				IL_0057: ldc.r4 6
				IL_005c: bge.un.s IL_0062

				IL_005e: ldloc.0
				IL_005f: ldc.i4.1
				IL_0060: add
				IL_0061: stloc.0

				IL_0062: ldloca.s 1
				IL_0064: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>::MoveNext()
				IL_0069: brtrue.s IL_0019
			// end loop

			IL_006b: leave.s IL_007b
		} // end .try
		finally
		{
			IL_006d: ldloca.s 1
			IL_006f: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class ['Assembly-CSharp']Actor>
			IL_0075: callvirt instance void [mscorlib]System.IDisposable::Dispose()
			IL_007a: endfinally
		} // end handler

		IL_007b: ldloc.0
		IL_007c: ret
	} // end of method VrUiTest::Sheep

	.method private hidebysig static 
		bool Select (
			string id
		) cil managed 
	{
		// Method begins at RVA 0x1ed7a0
		// Header size: 12
		// Code size: 101 (0x65)
		.maxstack 4
		.locals init (
			[0] class ['Assembly-CSharp']PowerButton,
			[1] class ['Assembly-CSharp']GodPower,
			[2] bool,
			[3] class [mscorlib]System.Exception
		)

		.try
		{
			IL_0000: ldarg.0
			IL_0001: call class ['Assembly-CSharp']PowerButton ['Assembly-CSharp']PowerButton::get(string)
			IL_0006: stloc.0
			IL_0007: ldloc.0
			IL_0008: ldnull
			IL_0009: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
			IL_000e: brfalse.s IL_001f

			IL_0010: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
			IL_0015: ldloc.0
			IL_0016: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::setPower(class ['Assembly-CSharp']PowerButton)
			IL_001b: ldc.i4.1
			IL_001c: stloc.2
			IL_001d: leave.s IL_0063

			IL_001f: ldsfld class ['Assembly-CSharp']PowerLibrary ['Assembly-CSharp']AssetManager::powers
			IL_0024: ldarg.0
			IL_0025: callvirt instance !0 class ['Assembly-CSharp']AssetLibrary`1<class ['Assembly-CSharp']GodPower>::get(string)
			IL_002a: stloc.1
			IL_002b: ldloc.1
			IL_002c: brtrue.s IL_0032

			IL_002e: ldc.i4.0
			IL_002f: stloc.2
			IL_0030: leave.s IL_0063

			IL_0032: ldsfld class ['Assembly-CSharp']PowerButtonSelector ['Assembly-CSharp']PowerButtonSelector::'instance'
			IL_0037: ldnull
			IL_0038: ldloc.1
			IL_0039: ldc.i4.0
			IL_003a: callvirt instance void ['Assembly-CSharp']PowerButtonSelector::setSelectedPower(class ['Assembly-CSharp']PowerButton, class ['Assembly-CSharp']GodPower, bool)
			IL_003f: ldc.i4.1
			IL_0040: stloc.2
			IL_0041: leave.s IL_0063
		} // end .try
		catch [mscorlib]System.Exception
		{
			IL_0043: stloc.3
			IL_0044: ldstr "VRUI select "
			IL_0049: ldarg.0
			IL_004a: ldstr ": "
			IL_004f: ldloc.3
			IL_0050: callvirt instance string [mscorlib]System.Exception::get_Message()
			IL_0055: call string [mscorlib]System.String::Concat(string, string, string, string)
			IL_005a: call void FirstPerson.Log::Error(string)
			IL_005f: ldc.i4.0
			IL_0060: stloc.2
			IL_0061: leave.s IL_0063
		} // end handler

		IL_0063: ldloc.2
		IL_0064: ret
	} // end of method VrUiTest::Select

	.method private hidebysig 
		instance class [mscorlib]System.Collections.IEnumerator Hold (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 'at',
			float32 zoom,
			float32 seconds
		) cil managed 
	{
		.custom instance void [mscorlib]System.Runtime.CompilerServices.IteratorStateMachineAttribute::.ctor(class [mscorlib]System.Type) = (
			01 00 20 46 69 72 73 74 50 65 72 73 6f 6e 2e 56
			72 55 69 54 65 73 74 2b 3c 48 6f 6c 64 3e 64 5f
			5f 32 30 00 00
		)
		// Method begins at RVA 0x1ed824
		// Header size: 1
		// Code size: 28 (0x1c)
		.maxstack 8

		IL_0000: ldc.i4.0
		IL_0001: newobj instance void FirstPerson.VrUiTest/'<Hold>d__20'::.ctor(int32)
		IL_0006: dup
		IL_0007: ldarg.1
		IL_0008: stfld valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 FirstPerson.VrUiTest/'<Hold>d__20'::'at'
		IL_000d: dup
		IL_000e: ldarg.2
		IL_000f: stfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::zoom
		IL_0014: dup
		IL_0015: ldarg.3
		IL_0016: stfld float32 FirstPerson.VrUiTest/'<Hold>d__20'::seconds
		IL_001b: ret
	} // end of method VrUiTest::Hold

	.method private hidebysig static 
		void HoldNow (
			valuetype [UnityEngine.CoreModule]UnityEngine.Vector2 'at',
			float32 zoom
		) cil managed 
	{
		// Method begins at RVA 0x1ed844
		// Header size: 12
		// Code size: 134 (0x86)
		.maxstack 4
		.locals init (
			[0] class [UnityEngine.CoreModule]UnityEngine.Camera,
			[1] class [mscorlib]System.Exception
		)

		.try
		{
			IL_0000: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0005: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Camera ['Assembly-CSharp']MapBox::get_camera()
			IL_000a: stloc.0
			IL_000b: ldloc.0
			IL_000c: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_0011: ldarg.0
			IL_0012: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::x
			IL_0017: ldarg.0
			IL_0018: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector2::y
			IL_001d: ldloc.0
			IL_001e: callvirt instance class [UnityEngine.CoreModule]UnityEngine.Transform [UnityEngine.CoreModule]UnityEngine.Component::get_transform()
			IL_0023: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Transform::get_position()
			IL_0028: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
			IL_002d: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
			IL_0032: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Transform::set_position(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
			IL_0037: ldloc.0
			IL_0038: ldarg.1
			IL_0039: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Camera::set_orthographicSize(float32)
			IL_003e: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0043: callvirt instance class ['Assembly-CSharp']MoveCamera ['Assembly-CSharp']MapBox::get_move_camera()
			IL_0048: ldnull
			IL_0049: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Inequality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
			IL_004e: brfalse.s IL_0060

			IL_0050: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0055: callvirt instance class ['Assembly-CSharp']MoveCamera ['Assembly-CSharp']MapBox::get_move_camera()
			IL_005a: ldarg.1
			IL_005b: callvirt instance void ['Assembly-CSharp']MoveCamera::setTargetZoom(float32)

			IL_0060: call class ['Assembly-CSharp']MapBox ['Assembly-CSharp']World::get_world()
			IL_0065: ldarg.1
			IL_0066: callvirt instance void ['Assembly-CSharp']MapBox::setZoomOrthographic(float32)
			IL_006b: leave.s IL_0085
		} // end .try
		catch [mscorlib]System.Exception
		{
			IL_006d: stloc.1
			IL_006e: ldstr "VRUI camera: "
			IL_0073: ldloc.1
			IL_0074: callvirt instance string [mscorlib]System.Exception::get_Message()
			IL_0079: call string [mscorlib]System.String::Concat(string, string)
			IL_007e: call void FirstPerson.Log::Error(string)
			IL_0083: leave.s IL_0085
		} // end handler

		IL_0085: ret
	} // end of method VrUiTest::HoldNow

	.method private hidebysig 
		instance void Head (
			valuetype FirstPerson.Vr.Xr/Quat q,
			[opt] float32 x,
			[opt] float32 y,
			[opt] float32 z
		) cil managed 
	{
		.param [2] = float32(0.0)
		.param [3] = float32(0.0)
		.param [4] = float32(0.0)
		// Method begins at RVA 0x1ed8e8
		// Header size: 12
		// Code size: 70 (0x46)
		.maxstack 4
		.locals init (
			[0] valuetype FirstPerson.Vr.Xr/Pose,
			[1] valuetype FirstPerson.Vr.Xr/Vec3
		)

		IL_0000: ldarg.0
		IL_0001: call instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
		IL_0006: ldloca.s 0
		IL_0008: initobj FirstPerson.Vr.Xr/Pose
		IL_000e: ldloca.s 0
		IL_0010: ldarg.1
		IL_0011: stfld valuetype FirstPerson.Vr.Xr/Quat FirstPerson.Vr.Xr/Pose::Orientation
		IL_0016: ldloca.s 0
		IL_0018: ldloca.s 1
		IL_001a: initobj FirstPerson.Vr.Xr/Vec3
		IL_0020: ldloca.s 1
		IL_0022: ldarg.2
		IL_0023: stfld float32 FirstPerson.Vr.Xr/Vec3::X
		IL_0028: ldloca.s 1
		IL_002a: ldarg.3
		IL_002b: stfld float32 FirstPerson.Vr.Xr/Vec3::Y
		IL_0030: ldloca.s 1
		IL_0032: ldarg.s z
		IL_0034: stfld float32 FirstPerson.Vr.Xr/Vec3::Z
		IL_0039: ldloc.1
		IL_003a: stfld valuetype FirstPerson.Vr.Xr/Vec3 FirstPerson.Vr.Xr/Pose::Position
		IL_003f: ldloc.0
		IL_0040: stfld valuetype FirstPerson.Vr.Xr/Pose FirstPerson.Vr.VrSystem::SimHead
		IL_0045: ret
	} // end of method VrUiTest::Head

	.method private hidebysig static 
		valuetype FirstPerson.Vr.Xr/Quat Quat (
			float32 x,
			float32 y,
			float32 z,
			float32 degrees
		) cil managed 
	{
		// Method begins at RVA 0x1ed93c
		// Header size: 12
		// Code size: 74 (0x4a)
		.maxstack 3
		.locals init (
			[0] float32,
			[1] float32,
			[2] valuetype FirstPerson.Vr.Xr/Quat
		)

		IL_0000: ldarg.3
		IL_0001: ldc.r4 0.5
		IL_0006: mul
		IL_0007: ldc.r4 0.017453292
		IL_000c: mul
		IL_000d: stloc.0
		IL_000e: ldloc.0
		IL_000f: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Sin(float32)
		IL_0014: stloc.1
		IL_0015: ldloca.s 2
		IL_0017: initobj FirstPerson.Vr.Xr/Quat
		IL_001d: ldloca.s 2
		IL_001f: ldarg.0
		IL_0020: ldloc.1
		IL_0021: mul
		IL_0022: stfld float32 FirstPerson.Vr.Xr/Quat::X
		IL_0027: ldloca.s 2
		IL_0029: ldarg.1
		IL_002a: ldloc.1
		IL_002b: mul
		IL_002c: stfld float32 FirstPerson.Vr.Xr/Quat::Y
		IL_0031: ldloca.s 2
		IL_0033: ldarg.2
		IL_0034: ldloc.1
		IL_0035: mul
		IL_0036: stfld float32 FirstPerson.Vr.Xr/Quat::Z
		IL_003b: ldloca.s 2
		IL_003d: ldloc.0
		IL_003e: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Cos(float32)
		IL_0043: stfld float32 FirstPerson.Vr.Xr/Quat::W
		IL_0048: ldloc.2
		IL_0049: ret
	} // end of method VrUiTest::Quat

	.method private hidebysig static 
		class [UnityEngine.CoreModule]UnityEngine.Texture2D Read (
			class [UnityEngine.CoreModule]UnityEngine.RenderTexture rt,
			bool headsetOrder
		) cil managed 
	{
		// Method begins at RVA 0x1ed994
		// Header size: 12
		// Code size: 170 (0xaa)
		.maxstack 6
		.locals init (
			[0] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
			[2] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
			[3] int32
		)

		IL_0000: ldarg.0
		IL_0001: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_0006: ldarg.0
		IL_0007: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_000c: ldc.i4.4
		IL_000d: ldc.i4.0
		IL_000e: newobj instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::.ctor(int32, int32, valuetype [UnityEngine.CoreModule]UnityEngine.TextureFormat, bool)
		IL_0013: stloc.0
		IL_0014: call class [UnityEngine.CoreModule]UnityEngine.RenderTexture [UnityEngine.CoreModule]UnityEngine.RenderTexture::get_active()
		IL_0019: ldarg.0
		IL_001a: call void [UnityEngine.CoreModule]UnityEngine.RenderTexture::set_active(class [UnityEngine.CoreModule]UnityEngine.RenderTexture)
		IL_001f: ldloc.0
		IL_0020: ldc.r4 0.0
		IL_0025: ldc.r4 0.0
		IL_002a: ldarg.0
		IL_002b: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_0030: conv.r4
		IL_0031: ldarg.0
		IL_0032: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_0037: conv.r4
		IL_0038: newobj instance void [UnityEngine.CoreModule]UnityEngine.Rect::.ctor(float32, float32, float32, float32)
		IL_003d: ldc.i4.0
		IL_003e: ldc.i4.0
		IL_003f: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::ReadPixels(valuetype [UnityEngine.CoreModule]UnityEngine.Rect, int32, int32)
		IL_0044: ldloc.0
		IL_0045: ldc.i4.0
		IL_0046: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::Apply(bool)
		IL_004b: call void [UnityEngine.CoreModule]UnityEngine.RenderTexture::set_active(class [UnityEngine.CoreModule]UnityEngine.RenderTexture)
		IL_0050: ldarg.1
		IL_0051: brfalse.s IL_00a8

		IL_0053: ldloc.0
		IL_0054: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color32[] [UnityEngine.CoreModule]UnityEngine.Texture2D::GetPixels32()
		IL_0059: stloc.1
		IL_005a: ldloc.1
		IL_005b: ldlen
		IL_005c: conv.i4
		IL_005d: newarr [UnityEngine.CoreModule]UnityEngine.Color32
		IL_0062: stloc.2
		IL_0063: ldc.i4.0
		IL_0064: stloc.3
		IL_0065: br.s IL_0091
		// loop start (head: IL_0091)
			IL_0067: ldloc.1
			IL_0068: ldloc.3
			IL_0069: ldarg.0
			IL_006a: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
			IL_006f: mul
			IL_0070: ldloc.2
			IL_0071: ldarg.0
			IL_0072: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
			IL_0077: ldc.i4.1
			IL_0078: sub
			IL_0079: ldloc.3
			IL_007a: sub
			IL_007b: ldarg.0
			IL_007c: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
			IL_0081: mul
			IL_0082: ldarg.0
			IL_0083: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
			IL_0088: call void [mscorlib]System.Array::Copy(class [mscorlib]System.Array, int32, class [mscorlib]System.Array, int32, int32)
			IL_008d: ldloc.3
			IL_008e: ldc.i4.1
			IL_008f: add
			IL_0090: stloc.3

			IL_0091: ldloc.3
			IL_0092: ldarg.0
			IL_0093: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
			IL_0098: blt.s IL_0067
		// end loop

		IL_009a: ldloc.0
		IL_009b: ldloc.2
		IL_009c: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::SetPixels32(valuetype [UnityEngine.CoreModule]UnityEngine.Color32[])
		IL_00a1: ldloc.0
		IL_00a2: ldc.i4.0
		IL_00a3: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::Apply(bool)

		IL_00a8: ldloc.0
		IL_00a9: ret
	} // end of method VrUiTest::Read

	.method private hidebysig 
		instance void SaveEyes (
			string name
		) cil managed 
	{
		// Method begins at RVA 0x1eda4c
		// Header size: 12
		// Code size: 287 (0x11f)
		.maxstack 6
		.locals init (
			[0] class [UnityEngine.CoreModule]UnityEngine.RenderTexture,
			[1] class [UnityEngine.CoreModule]UnityEngine.RenderTexture,
			[2] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
			[3] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
			[4] int32,
			[5] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
			[6] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
			[7] int32
		)

		IL_0000: ldarg.0
		IL_0001: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
		IL_0006: ldc.i4.0
		IL_0007: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::VrEyeForTest(int32)
		IL_000c: stloc.0
		IL_000d: ldarg.0
		IL_000e: ldfld class FirstPerson.WorldBoxMod FirstPerson.CivilTest::Mod
		IL_0013: ldc.i4.1
		IL_0014: callvirt instance class [UnityEngine.CoreModule]UnityEngine.RenderTexture FirstPerson.WorldBoxMod::VrEyeForTest(int32)
		IL_0019: stloc.1
		IL_001a: ldloc.0
		IL_001b: ldnull
		IL_001c: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0021: brtrue.s IL_002c

		IL_0023: ldloc.1
		IL_0024: ldnull
		IL_0025: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_002a: brfalse.s IL_003e

		IL_002c: ldarg.0
		IL_002d: ldstr "no eye pictures to save for "
		IL_0032: ldarg.1
		IL_0033: call string [mscorlib]System.String::Concat(string, string)
		IL_0038: call instance void FirstPerson.CivilTest::Note(string)
		IL_003d: ret

		IL_003e: ldloc.0
		IL_003f: ldc.i4.1
		IL_0040: call class [UnityEngine.CoreModule]UnityEngine.Texture2D FirstPerson.VrUiTest::Read(class [UnityEngine.CoreModule]UnityEngine.RenderTexture, bool)
		IL_0045: stloc.2
		IL_0046: ldloc.1
		IL_0047: ldc.i4.1
		IL_0048: call class [UnityEngine.CoreModule]UnityEngine.Texture2D FirstPerson.VrUiTest::Read(class [UnityEngine.CoreModule]UnityEngine.RenderTexture, bool)
		IL_004d: stloc.3
		IL_004e: ldloc.2
		IL_004f: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_0054: ldloc.3
		IL_0055: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_005a: add
		IL_005b: ldc.i4.8
		IL_005c: add
		IL_005d: ldloc.2
		IL_005e: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_0063: ldloc.3
		IL_0064: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_0069: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::Max(int32, int32)
		IL_006e: stloc.s 4
		IL_0070: dup
		IL_0071: ldloc.s 4
		IL_0073: ldc.i4.4
		IL_0074: ldc.i4.0
		IL_0075: newobj instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::.ctor(int32, int32, valuetype [UnityEngine.CoreModule]UnityEngine.TextureFormat, bool)
		IL_007a: stloc.s 5
		IL_007c: ldloc.s 4
		IL_007e: mul
		IL_007f: newarr [UnityEngine.CoreModule]UnityEngine.Color32
		IL_0084: stloc.s 6
		IL_0086: ldc.i4.0
		IL_0087: stloc.s 7
		IL_0089: br.s IL_00aa
		// loop start (head: IL_00aa)
			IL_008b: ldloc.s 6
			IL_008d: ldloc.s 7
			IL_008f: ldc.i4.s 20
			IL_0091: ldc.i4.s 20
			IL_0093: ldc.i4.s 20
			IL_0095: ldc.i4 255
			IL_009a: newobj instance void [UnityEngine.CoreModule]UnityEngine.Color32::.ctor(uint8, uint8, uint8, uint8)
			IL_009f: stelem [UnityEngine.CoreModule]UnityEngine.Color32
			IL_00a4: ldloc.s 7
			IL_00a6: ldc.i4.1
			IL_00a7: add
			IL_00a8: stloc.s 7

			IL_00aa: ldloc.s 7
			IL_00ac: ldloc.s 6
			IL_00ae: ldlen
			IL_00af: conv.i4
			IL_00b0: blt.s IL_008b
		// end loop

		IL_00b2: ldloc.s 5
		IL_00b4: ldloc.s 6
		IL_00b6: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::SetPixels32(valuetype [UnityEngine.CoreModule]UnityEngine.Color32[])
		IL_00bb: ldloc.s 5
		IL_00bd: ldc.i4.0
		IL_00be: ldc.i4.0
		IL_00bf: ldloc.2
		IL_00c0: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_00c5: ldloc.2
		IL_00c6: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_00cb: ldloc.2
		IL_00cc: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color32[] [UnityEngine.CoreModule]UnityEngine.Texture2D::GetPixels32()
		IL_00d1: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::SetPixels32(int32, int32, int32, int32, valuetype [UnityEngine.CoreModule]UnityEngine.Color32[])
		IL_00d6: ldloc.s 5
		IL_00d8: ldloc.2
		IL_00d9: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_00de: ldc.i4.8
		IL_00df: add
		IL_00e0: ldc.i4.0
		IL_00e1: ldloc.3
		IL_00e2: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_00e7: ldloc.3
		IL_00e8: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_00ed: ldloc.3
		IL_00ee: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color32[] [UnityEngine.CoreModule]UnityEngine.Texture2D::GetPixels32()
		IL_00f3: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::SetPixels32(int32, int32, int32, int32, valuetype [UnityEngine.CoreModule]UnityEngine.Color32[])
		IL_00f8: ldloc.s 5
		IL_00fa: ldc.i4.0
		IL_00fb: callvirt instance void [UnityEngine.CoreModule]UnityEngine.Texture2D::Apply(bool)
		IL_0100: ldloc.s 5
		IL_0102: ldarg.1
		IL_0103: ldstr ".png"
		IL_0108: call string [mscorlib]System.String::Concat(string, string)
		IL_010d: call void FirstPerson.VrUiTest::Save(class [UnityEngine.CoreModule]UnityEngine.Texture2D, string)
		IL_0112: ldloc.2
		IL_0113: call void [UnityEngine.CoreModule]UnityEngine.Object::Destroy(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0118: ldloc.3
		IL_0119: call void [UnityEngine.CoreModule]UnityEngine.Object::Destroy(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_011e: ret
	} // end of method VrUiTest::SaveEyes

	.method private hidebysig static 
		void Save (
			class [UnityEngine.CoreModule]UnityEngine.Texture2D t,
			string name
		) cil managed 
	{
		// Method begins at RVA 0x1edb77
		// Header size: 1
		// Code size: 45 (0x2d)
		.maxstack 8

		IL_0000: call string FirstPerson.Log::get_Folder()
		IL_0005: ldarg.1
		IL_0006: call string [mscorlib]System.IO.Path::Combine(string, string)
		IL_000b: ldarg.0
		IL_000c: call uint8[] [UnityEngine.ImageConversionModule]UnityEngine.ImageConversion::EncodeToPNG(class [UnityEngine.CoreModule]UnityEngine.Texture2D)
		IL_0011: call void [mscorlib]System.IO.File::WriteAllBytes(string, uint8[])
		IL_0016: ldstr "VRUI saved "
		IL_001b: ldarg.1
		IL_001c: call string [mscorlib]System.String::Concat(string, string)
		IL_0021: call void FirstPerson.Log::Info(string)
		IL_0026: ldarg.0
		IL_0027: call void [UnityEngine.CoreModule]UnityEngine.Object::Destroy(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_002c: ret
	} // end of method VrUiTest::Save

	.method private hidebysig static 
		string Show (
			valuetype [UnityEngine.CoreModule]UnityEngine.Color c
		) cil managed 
	{
		// Method begins at RVA 0x1edba5
		// Header size: 1
		// Code size: 62 (0x3e)
		.maxstack 8

		IL_0000: ldstr "{0:F0},{1:F0},{2:F0}"
		IL_0005: ldarg.0
		IL_0006: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::r
		IL_000b: ldc.r4 255
		IL_0010: mul
		IL_0011: box [mscorlib]System.Single
		IL_0016: ldarg.0
		IL_0017: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::g
		IL_001c: ldc.r4 255
		IL_0021: mul
		IL_0022: box [mscorlib]System.Single
		IL_0027: ldarg.0
		IL_0028: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Color::b
		IL_002d: ldc.r4 255
		IL_0032: mul
		IL_0033: box [mscorlib]System.Single
		IL_0038: call string [mscorlib]System.String::Format(string, object, object, object)
		IL_003d: ret
	} // end of method VrUiTest::Show

	.method private hidebysig static 
		float32 SeamUnder (
			class [UnityEngine.CoreModule]UnityEngine.RenderTexture rt
		) cil managed 
	{
		// Method begins at RVA 0x1edbe4
		// Header size: 12
		// Code size: 487 (0x1e7)
		.maxstack 6
		.locals init (
			[0] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
			[2] int32,
			[3] int32,
			[4] class [mscorlib]System.Collections.Generic.List`1<float32>,
			[5] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3[],
			[6] float32,
			[7] int32,
			[8] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3[],
			[9] int32,
			[10] float32,
			[11] float32,
			[12] float32,
			[13] float32,
			[14] int32,
			[15] valuetype [UnityEngine.CoreModule]UnityEngine.Color32,
			[16] float32,
			[17] int32,
			[18] valuetype [UnityEngine.CoreModule]UnityEngine.Vector3,
			[19] valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<float32>,
			[20] float32
		)

		IL_0000: ldarg.0
		IL_0001: ldnull
		IL_0002: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0007: brfalse.s IL_000f

		IL_0009: ldc.r4 0.0
		IL_000e: ret

		IL_000f: ldarg.0
		IL_0010: ldc.i4.1
		IL_0011: call class [UnityEngine.CoreModule]UnityEngine.Texture2D FirstPerson.VrUiTest::Read(class [UnityEngine.CoreModule]UnityEngine.RenderTexture, bool)
		IL_0016: stloc.0
		IL_0017: ldloc.0
		IL_0018: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color32[] [UnityEngine.CoreModule]UnityEngine.Texture2D::GetPixels32()
		IL_001d: stloc.1
		IL_001e: ldloc.0
		IL_001f: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_0024: ldc.i4.2
		IL_0025: mul
		IL_0026: ldc.i4.5
		IL_0027: div
		IL_0028: ldloc.0
		IL_0029: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_002e: ldc.i4.3
		IL_002f: mul
		IL_0030: ldc.i4.5
		IL_0031: div
		IL_0032: stloc.2
		IL_0033: ldloc.0
		IL_0034: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
		IL_0039: ldc.i4.3
		IL_003a: div
		IL_003b: stloc.3
		IL_003c: newobj instance void class [mscorlib]System.Collections.Generic.List`1<float32>::.ctor()
		IL_0041: stloc.s 4
		IL_0043: ldnull
		IL_0044: stloc.s 5
		IL_0046: stloc.s 7
		IL_0048: br IL_0170
		// loop start (head: IL_0170)
			IL_004d: ldc.i4.3
			IL_004e: newarr [UnityEngine.CoreModule]UnityEngine.Vector3
			IL_0053: stloc.s 8
			IL_0055: ldc.i4.0
			IL_0056: stloc.s 9
			IL_0058: br IL_00f2
			// loop start (head: IL_00f2)
				IL_005d: ldc.r4 0.0
				IL_0062: stloc.s 10
				IL_0064: ldc.r4 0.0
				IL_0069: stloc.s 11
				IL_006b: ldc.r4 0.0
				IL_0070: stloc.s 12
				IL_0072: ldloc.s 9
				IL_0074: ldloc.3
				IL_0075: mul
				IL_0076: stloc.s 14
				IL_0078: br.s IL_00bb
				// loop start (head: IL_00bb)
					IL_007a: ldloc.1
					IL_007b: ldloc.s 7
					IL_007d: ldloc.0
					IL_007e: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
					IL_0083: mul
					IL_0084: ldloc.s 14
					IL_0086: add
					IL_0087: ldelem [UnityEngine.CoreModule]UnityEngine.Color32
					IL_008c: stloc.s 15
					IL_008e: ldloc.s 10
					IL_0090: ldloc.s 15
					IL_0092: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::r
					IL_0097: conv.r4
					IL_0098: add
					IL_0099: stloc.s 10
					IL_009b: ldloc.s 11
					IL_009d: ldloc.s 15
					IL_009f: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::g
					IL_00a4: conv.r4
					IL_00a5: add
					IL_00a6: stloc.s 11
					IL_00a8: ldloc.s 12
					IL_00aa: ldloc.s 15
					IL_00ac: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::b
					IL_00b1: conv.r4
					IL_00b2: add
					IL_00b3: stloc.s 12
					IL_00b5: ldloc.s 14
					IL_00b7: ldc.i4.1
					IL_00b8: add
					IL_00b9: stloc.s 14

					IL_00bb: ldloc.s 14
					IL_00bd: ldloc.s 9
					IL_00bf: ldc.i4.1
					IL_00c0: add
					IL_00c1: ldloc.3
					IL_00c2: mul
					IL_00c3: blt.s IL_007a
				// end loop

				IL_00c5: ldc.r4 255
				IL_00ca: ldloc.3
				IL_00cb: conv.r4
				IL_00cc: mul
				IL_00cd: stloc.s 13
				IL_00cf: ldloc.s 8
				IL_00d1: ldloc.s 9
				IL_00d3: ldloc.s 10
				IL_00d5: ldloc.s 13
				IL_00d7: div
				IL_00d8: ldloc.s 11
				IL_00da: ldloc.s 13
				IL_00dc: div
				IL_00dd: ldloc.s 12
				IL_00df: ldloc.s 13
				IL_00e1: div
				IL_00e2: newobj instance void [UnityEngine.CoreModule]UnityEngine.Vector3::.ctor(float32, float32, float32)
				IL_00e7: stelem [UnityEngine.CoreModule]UnityEngine.Vector3
				IL_00ec: ldloc.s 9
				IL_00ee: ldc.i4.1
				IL_00ef: add
				IL_00f0: stloc.s 9

				IL_00f2: ldloc.s 9
				IL_00f4: ldc.i4.3
				IL_00f5: blt IL_005d
			// end loop

			IL_00fa: ldloc.s 5
			IL_00fc: brfalse.s IL_0166

			IL_00fe: ldc.r4 3.4028235E+38
			IL_0103: stloc.s 16
			IL_0105: ldc.i4.0
			IL_0106: stloc.s 17
			IL_0108: br.s IL_0158
			// loop start (head: IL_0158)
				IL_010a: ldloc.s 8
				IL_010c: ldloc.s 17
				IL_010e: ldelem [UnityEngine.CoreModule]UnityEngine.Vector3
				IL_0113: ldloc.s 5
				IL_0115: ldloc.s 17
				IL_0117: ldelem [UnityEngine.CoreModule]UnityEngine.Vector3
				IL_011c: call valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Vector3::op_Subtraction(valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3)
				IL_0121: stloc.s 18
				IL_0123: ldloc.s 16
				IL_0125: ldloc.s 18
				IL_0127: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::x
				IL_012c: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
				IL_0131: ldloc.s 18
				IL_0133: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::y
				IL_0138: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
				IL_013d: add
				IL_013e: ldloc.s 18
				IL_0140: ldfld float32 [UnityEngine.CoreModule]UnityEngine.Vector3::z
				IL_0145: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Abs(float32)
				IL_014a: add
				IL_014b: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Min(float32, float32)
				IL_0150: stloc.s 16
				IL_0152: ldloc.s 17
				IL_0154: ldc.i4.1
				IL_0155: add
				IL_0156: stloc.s 17

				IL_0158: ldloc.s 17
				IL_015a: ldc.i4.3
				IL_015b: blt.s IL_010a
			// end loop

			IL_015d: ldloc.s 4
			IL_015f: ldloc.s 16
			IL_0161: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<float32>::Add(!0)

			IL_0166: ldloc.s 8
			IL_0168: stloc.s 5
			IL_016a: ldloc.s 7
			IL_016c: ldc.i4.1
			IL_016d: add
			IL_016e: stloc.s 7

			IL_0170: ldloc.s 7
			IL_0172: ldloc.2
			IL_0173: blt IL_004d
		// end loop

		IL_0178: ldloc.0
		IL_0179: call void [UnityEngine.CoreModule]UnityEngine.Object::Destroy(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_017e: ldloc.s 4
		IL_0180: callvirt instance int32 class [mscorlib]System.Collections.Generic.List`1<float32>::get_Count()
		IL_0185: brtrue.s IL_018d

		IL_0187: ldc.r4 0.0
		IL_018c: ret

		IL_018d: ldc.r4 0.0
		IL_0192: stloc.s 6
		IL_0194: ldloc.s 4
		IL_0196: callvirt instance valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<!0> class [mscorlib]System.Collections.Generic.List`1<float32>::GetEnumerator()
		IL_019b: stloc.s 19
		.try
		{
			IL_019d: br.s IL_01b3
			// loop start (head: IL_01b3)
				IL_019f: ldloca.s 19
				IL_01a1: call instance !0 valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<float32>::get_Current()
				IL_01a6: stloc.s 20
				IL_01a8: ldloc.s 6
				IL_01aa: ldloc.s 20
				IL_01ac: call float32 [UnityEngine.CoreModule]UnityEngine.Mathf::Max(float32, float32)
				IL_01b1: stloc.s 6

				IL_01b3: ldloca.s 19
				IL_01b5: call instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<float32>::MoveNext()
				IL_01ba: brtrue.s IL_019f
			// end loop

			IL_01bc: leave.s IL_01cc
		} // end .try
		finally
		{
			IL_01be: ldloca.s 19
			IL_01c0: constrained. valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<float32>
			IL_01c6: callvirt instance void [mscorlib]System.IDisposable::Dispose()
			IL_01cb: endfinally
		} // end handler

		IL_01cc: ldloc.s 4
		IL_01ce: callvirt instance void class [mscorlib]System.Collections.Generic.List`1<float32>::Sort()
		IL_01d3: ldloc.s 6
		IL_01d5: ldloc.s 4
		IL_01d7: ldloc.s 4
		IL_01d9: callvirt instance int32 class [mscorlib]System.Collections.Generic.List`1<float32>::get_Count()
		IL_01de: ldc.i4.2
		IL_01df: div
		IL_01e0: callvirt instance !0 class [mscorlib]System.Collections.Generic.List`1<float32>::get_Item(int32)
		IL_01e5: sub
		IL_01e6: ret
	} // end of method VrUiTest::SeamUnder

	.method private hidebysig static 
		valuetype [UnityEngine.CoreModule]UnityEngine.Color Middle (
			class [UnityEngine.CoreModule]UnityEngine.RenderTexture rt
		) cil managed 
	{
		// Method begins at RVA 0x1edde8
		// Header size: 12
		// Code size: 237 (0xed)
		.maxstack 4
		.locals init (
			[0] class [UnityEngine.CoreModule]UnityEngine.Texture2D,
			[1] valuetype [UnityEngine.CoreModule]UnityEngine.Color32[],
			[2] float32,
			[3] float32,
			[4] float32,
			[5] int32,
			[6] int32,
			[7] int32,
			[8] valuetype [UnityEngine.CoreModule]UnityEngine.Color32
		)

		IL_0000: ldarg.0
		IL_0001: ldnull
		IL_0002: call bool [UnityEngine.CoreModule]UnityEngine.Object::op_Equality(class [UnityEngine.CoreModule]UnityEngine.Object, class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_0007: brfalse.s IL_000f

		IL_0009: call valuetype [UnityEngine.CoreModule]UnityEngine.Color [UnityEngine.CoreModule]UnityEngine.Color::get_black()
		IL_000e: ret

		IL_000f: ldarg.0
		IL_0010: ldc.i4.1
		IL_0011: call class [UnityEngine.CoreModule]UnityEngine.Texture2D FirstPerson.VrUiTest::Read(class [UnityEngine.CoreModule]UnityEngine.RenderTexture, bool)
		IL_0016: stloc.0
		IL_0017: ldloc.0
		IL_0018: callvirt instance valuetype [UnityEngine.CoreModule]UnityEngine.Color32[] [UnityEngine.CoreModule]UnityEngine.Texture2D::GetPixels32()
		IL_001d: stloc.1
		IL_001e: ldc.r4 0.0
		IL_0023: stloc.2
		IL_0024: ldc.r4 0.0
		IL_0029: stloc.3
		IL_002a: ldc.r4 0.0
		IL_002f: stloc.s 4
		IL_0031: ldc.i4.0
		IL_0032: stloc.s 5
		IL_0034: ldloc.0
		IL_0035: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
		IL_003a: ldc.i4.2
		IL_003b: mul
		IL_003c: ldc.i4.5
		IL_003d: div
		IL_003e: stloc.s 6
		IL_0040: br.s IL_00a7
		// loop start (head: IL_00a7)
			IL_0042: ldloc.0
			IL_0043: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
			IL_0048: ldc.i4.2
			IL_0049: mul
			IL_004a: ldc.i4.5
			IL_004b: div
			IL_004c: stloc.s 7
			IL_004e: br.s IL_0093
			// loop start (head: IL_0093)
				IL_0050: ldloc.1
				IL_0051: ldloc.s 6
				IL_0053: ldloc.0
				IL_0054: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
				IL_0059: mul
				IL_005a: ldloc.s 7
				IL_005c: add
				IL_005d: ldelem [UnityEngine.CoreModule]UnityEngine.Color32
				IL_0062: stloc.s 8
				IL_0064: ldloc.2
				IL_0065: ldloc.s 8
				IL_0067: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::r
				IL_006c: conv.r4
				IL_006d: add
				IL_006e: stloc.2
				IL_006f: ldloc.3
				IL_0070: ldloc.s 8
				IL_0072: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::g
				IL_0077: conv.r4
				IL_0078: add
				IL_0079: stloc.3
				IL_007a: ldloc.s 4
				IL_007c: ldloc.s 8
				IL_007e: ldfld uint8 [UnityEngine.CoreModule]UnityEngine.Color32::b
				IL_0083: conv.r4
				IL_0084: add
				IL_0085: stloc.s 4
				IL_0087: ldloc.s 5
				IL_0089: ldc.i4.1
				IL_008a: add
				IL_008b: stloc.s 5
				IL_008d: ldloc.s 7
				IL_008f: ldc.i4.1
				IL_0090: add
				IL_0091: stloc.s 7

				IL_0093: ldloc.s 7
				IL_0095: ldloc.0
				IL_0096: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_width()
				IL_009b: ldc.i4.3
				IL_009c: mul
				IL_009d: ldc.i4.5
				IL_009e: div
				IL_009f: blt.s IL_0050
			// end loop

			IL_00a1: ldloc.s 6
			IL_00a3: ldc.i4.1
			IL_00a4: add
			IL_00a5: stloc.s 6

			IL_00a7: ldloc.s 6
			IL_00a9: ldloc.0
			IL_00aa: callvirt instance int32 [UnityEngine.CoreModule]UnityEngine.Texture::get_height()
			IL_00af: ldc.i4.3
			IL_00b0: mul
			IL_00b1: ldc.i4.5
			IL_00b2: div
			IL_00b3: blt.s IL_0042
		// end loop

		IL_00b5: ldloc.0
		IL_00b6: call void [UnityEngine.CoreModule]UnityEngine.Object::Destroy(class [UnityEngine.CoreModule]UnityEngine.Object)
		IL_00bb: ldc.i4.1
		IL_00bc: ldloc.s 5
		IL_00be: call int32 [UnityEngine.CoreModule]UnityEngine.Mathf::Max(int32, int32)
		IL_00c3: stloc.s 5
		IL_00c5: ldloc.2
		IL_00c6: ldloc.s 5
		IL_00c8: conv.r4
		IL_00c9: div
		IL_00ca: ldc.r4 255
		IL_00cf: div
		IL_00d0: ldloc.3
		IL_00d1: ldloc.s 5
		IL_00d3: conv.r4
		IL_00d4: div
		IL_00d5: ldc.r4 255
		IL_00da: div
		IL_00db: ldloc.s 4
		IL_00dd: ldloc.s 5
		IL_00df: conv.r4
		IL_00e0: div
		IL_00e1: ldc.r4 255
		IL_00e6: div
		IL_00e7: newobj instance void [UnityEngine.CoreModule]UnityEngine.Color::.ctor(float32, float32, float32)
		IL_00ec: ret
	} // end of method VrUiTest::Middle

	.method public hidebysig specialname rtspecialname 
		instance void .ctor () cil managed 
	{
		// Method begins at RVA 0x1edee1
		// Header size: 1
		// Code size: 19 (0x13)
		.maxstack 8

		IL_0000: ldarg.0
		IL_0001: ldc.i4.3
		IL_0002: newarr [UnityEngine.CoreModule]UnityEngine.RenderTexture
		IL_0007: stfld class [UnityEngine.CoreModule]UnityEngine.RenderTexture[] FirstPerson.VrUiTest::_images
		IL_000c: ldarg.0
		IL_000d: call instance void FirstPerson.CivilTest::.ctor()
		IL_0012: ret
	} // end of method VrUiTest::.ctor

	// Properties
	.property instance string Prefix()
	{
		.get instance string FirstPerson.VrUiTest::get_Prefix()
	}
	.property instance class FirstPerson.Vr.VrSystem Vr()
	{
		.get instance class FirstPerson.Vr.VrSystem FirstPerson.VrUiTest::get_Vr()
	}

} // end of class FirstPerson.VrUiTest

