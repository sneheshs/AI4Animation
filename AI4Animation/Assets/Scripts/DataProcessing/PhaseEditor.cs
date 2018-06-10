﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(MotionEditor))]
public class PhaseEditor : MonoBehaviour {

	public MotionEditor Editor = null;
	public PhaseModule[] Modules = new PhaseModule[0];

	public MotionEditor GetEditor() {
		if(Editor == null) {
			Editor = GetComponent<MotionEditor>();
		}
		return Editor;
	}

	public PhaseModule GetModule() {
		if(Modules.Length == 0) {
			return null;
		}
		return Modules[GetEditor().GetID()];
	}

	public void Refresh() {
		MotionData[] files = GetEditor().GetFiles();
		foreach(PhaseModule module in Modules) {
			if(!System.Array.Find(files, x => x == module.Data)) {
				ArrayExtensions.Remove(ref Modules, module);
			}
		}
		PhaseModule[] modules = new PhaseModule[GetEditor().GetFiles().Length];
		for(int i=0; i<files.Length; i++) {
			modules[i] = System.Array.Find(Modules, x => x.Data == files[i]);
			if(modules[i] == null) {
				modules[i] = new PhaseModule(files[i]);
			}
		}
		Modules = modules;
	}

	[System.Serializable]
	public class PhaseModule {
		public MotionData Data;
		
		public float[] RegularPhase;
		public float[] InversePhase;

		public bool[] Variables;
		public float MaximumVelocity = 5f;
		public float VelocityThreshold = 0.1f;
		public bool ShowVelocities = true;
		public bool ShowCycle = true;
		public float TimeWindow;

		public PhaseFunction RegularPhaseFunction;
		public PhaseFunction InversePhaseFunction;
		public bool Optimising;

		public PhaseModule(MotionData data) {
			Data = data;
			TimeWindow = data.GetTotalTime();
			RegularPhase = new float[data.GetTotalFrames()];
			InversePhase = new float[data.GetTotalFrames()];

			Variables = new bool[data.Source.Bones.Length];
		}

		public PhaseFunction GetRegularPhaseFunction() {
			if(RegularPhaseFunction == null) {
				RegularPhaseFunction = new PhaseFunction(this, RegularPhase);
			}
			return RegularPhaseFunction;
		}

		public PhaseFunction GetInversePhaseFunction() {
			if(InversePhaseFunction == null) {
				InversePhaseFunction = new PhaseFunction(this, InversePhase);
			}
			return InversePhaseFunction;
		}

		public void SetMaximumVelocity(float value) {
			value = Mathf.Max(1f, value);
			if(MaximumVelocity != value) {
				MaximumVelocity = value;
				GetRegularPhaseFunction().ComputeVelocities();
				GetInversePhaseFunction().ComputeVelocities();
			}
		}

		public void SetVelocityThreshold(float value) {
			value = Mathf.Max(0f, value);
			if(VelocityThreshold != value) {
				VelocityThreshold = value;
				GetRegularPhaseFunction().ComputeVelocities();
				GetInversePhaseFunction().ComputeVelocities();
			}
		}

		public void ToggleVariable(int index) {
			Variables[index] = !Variables[index];
			GetRegularPhaseFunction().ComputeVelocities();
			GetInversePhaseFunction().ComputeVelocities();
		}
	}

	public class PhaseFunction {
		public PhaseModule Module;

		public float[] Phase;
		public bool[] Keys;
		public float[] Cycle;
		public float[] NormalisedCycle;

		public float[] RegularVelocities;
		public float[] InverseVelocities;
		public float[] RegularNormalisedVelocities;
		public float[] InverseNormalisedVelocities;

		public PhaseEvolution Optimiser;

		public PhaseFunction(PhaseModule module, float[] values) {
			Module = module;

			int frames = module.Data.GetTotalFrames();
			int bones = module.Data.Source.Bones.Length;

			Phase = values.Length != frames ? new float[frames] : values;
			Keys = new bool[frames];
			Cycle = new float[frames];
			NormalisedCycle = new float[frames];

			for(int i=0; i<Phase.Length; i++) {
				Keys[i] = Phase[i] == 1f;
			}

			ComputeVelocities();
		}

		public void ComputeVelocities() {
			float min, max;

			RegularVelocities = new float[Module.Data.GetTotalFrames()];
			RegularNormalisedVelocities = new float[Module.Data.GetTotalFrames()];
			min = float.MaxValue;
			max = float.MinValue;
			for(int i=0; i<RegularVelocities.Length; i++) {
				for(int j=0; j<Module.Variables.Length; j++) {
					if(Module.Variables[j]) {
						float boneVelocity = Mathf.Min(Module.Data.Frames[i].GetBoneVelocity(j, false).magnitude, Module.MaximumVelocity);
						RegularVelocities[i] += boneVelocity;
					}
				}
				if(RegularVelocities[i] < Module.VelocityThreshold) {
					RegularVelocities[i] = 0f;
				}
				if(RegularVelocities[i] < min) {
					min = RegularVelocities[i];
				}
				if(RegularVelocities[i] > max) {
					max = RegularVelocities[i];
				}
			}
			for(int i=0; i<RegularVelocities.Length; i++) {
				RegularNormalisedVelocities[i] = Utility.Normalise(RegularVelocities[i], min, max, 0f, 1f);
			}

			InverseVelocities = new float[Module.Data.GetTotalFrames()];
			InverseNormalisedVelocities = new float[Module.Data.GetTotalFrames()];
			min = float.MaxValue;
			max = float.MinValue;
			for(int i=0; i<InverseVelocities.Length; i++) {
				for(int j=0; j<Module.Variables.Length; j++) {
					if(Module.Variables[Module.Data.Symmetry[j]]) {
						float boneVelocity = Mathf.Min(Module.Data.Frames[i].GetBoneVelocity(j, false).magnitude, Module.MaximumVelocity);
						InverseVelocities[i] += boneVelocity;
					}
				}
				if(InverseVelocities[i] < Module.VelocityThreshold) {
					InverseVelocities[i] = 0f;
				}
				if(InverseVelocities[i] < min) {
					min = InverseVelocities[i];
				}
				if(InverseVelocities[i] > max) {
					max = InverseVelocities[i];
				}
			}
			for(int i=0; i<InverseVelocities.Length; i++) {
				InverseNormalisedVelocities[i] = Utility.Normalise(InverseVelocities[i], min, max, 0f, 1f);
			}
		}

		public void Clear() {
			for(int i=0; i<Phase.Length; i++) {
				Phase[i] = 0f;
				Keys[i] = false;
				Cycle[i] = 0f;
				NormalisedCycle[i] = 0f;
			}
		}

		public void Save() {
			if(this == Module.GetRegularPhaseFunction()) {
				Module.RegularPhase = (float[])Phase.Clone();
			} else {
				Module.InversePhase = (float[])Phase.Clone();
			}
		}

		public void SetKey(MotionData.Frame frame, bool value) {
			if(value) {
				if(IsKey(frame)) {
					return;
				}
				Keys[frame.Index-1] = true;
				Phase[frame.Index-1] = 1f;
				Interpolate(frame);
			} else {
				if(!IsKey(frame)) {
					return;
				}
				Keys[frame.Index-1] = false;
				Phase[frame.Index-1] = 0f;
				Interpolate(frame);
			}
		}

		public bool IsKey(MotionData.Frame frame) {
			return Keys[frame.Index-1];
		}

		public void SetPhase(MotionData.Frame frame, float value) {
			if(Phase[frame.Index-1] != value) {
				Phase[frame.Index-1] = value;
				Interpolate(frame);
			}
		}

		public float GetPhase(MotionData.Frame frame) {
			return Phase[frame.Index-1];
		}

		public MotionData.Frame GetPreviousKey(MotionData.Frame frame) {
			if(frame != null) {
				for(int i=frame.Index-1; i>=1; i--) {
					if(Keys[i-1]) {
						return Module.Data.Frames[i-1];
					}
				}
			}
			return Module.Data.Frames[0];
		}

		public MotionData.Frame GetNextKey(MotionData.Frame frame) {
			if(frame != null) {
				for(int i=frame.Index+1; i<=Module.Data.GetTotalFrames(); i++) {
					if(Keys[i-1]) {
						return Module.Data.Frames[i-1];
					}
				}
			}
			return Module.Data.Frames[Module.Data.GetTotalFrames()-1];
		}

		public void Recompute() {
			for(int i=0; i<Module.Data.Frames.Length; i++) {
				if(IsKey(Module.Data.Frames[i])) {
					Phase[i] = 1f;
				}
			}
			MotionData.Frame A = Module.Data.Frames[0];
			MotionData.Frame B = GetNextKey(A);
			while(A != B) {
				Interpolate(A, B);
				A = B;
				B = GetNextKey(A);
			}
		}

		private void Interpolate(MotionData.Frame frame) {
			if(IsKey(frame)) {
				Interpolate(GetPreviousKey(frame), frame);
				Interpolate(frame, GetNextKey(frame));
			} else {
				Interpolate(GetPreviousKey(frame), GetNextKey(frame));
			}
		}

		private void Interpolate(MotionData.Frame a, MotionData.Frame b) {
			if(a == null || b == null) {
				Debug.Log("A given frame was null.");
				return;
			}
			int dist = b.Index - a.Index;
			if(dist >= 2) {
				for(int i=a.Index+1; i<b.Index; i++) {
					float rateA = (float)((float)i-(float)a.Index)/(float)dist;
					float rateB = (float)((float)b.Index-(float)i)/(float)dist;
					Phase[i-1] = rateB*Mathf.Repeat(Phase[a.Index-1], 1f) + rateA*Phase[b.Index-1];
				}
			}

			if(a.Index == 1) {
				MotionData.Frame first = Module.Data.Frames[0];
				MotionData.Frame next1 = GetNextKey(first);
				MotionData.Frame next2 = GetNextKey(next1);
				Keys[0] = true;
				float xFirst = next1.Timestamp - first.Timestamp;
				float mFirst = next2.Timestamp - next1.Timestamp;
				SetPhase(first, Mathf.Clamp(1f - xFirst / mFirst, 0f, 1f));
			}
			if(b.Index == Module.Data.GetTotalFrames()) {
				MotionData.Frame last = Module.Data.Frames[Module.Data.GetTotalFrames()-1];
				MotionData.Frame previous1 = GetPreviousKey(last);
				MotionData.Frame previous2 = GetPreviousKey(previous1);
				Keys[Module.Data.GetTotalFrames()-1] = true;
				float xLast = last.Timestamp - previous1.Timestamp;
				float mLast = previous1.Timestamp - previous2.Timestamp;
				SetPhase(last, Mathf.Clamp(xLast / mLast, 0f, 1f));
			}
		}

		public void EditorUpdate() {
			if(Module.Optimising) {
				if(Optimiser == null) {
					Optimiser = new PhaseEvolution(this);
				}
				Optimiser.Optimise();
			} else {
				Optimiser = null;
			}
		}

		public void Inspector() {
			UltiDraw.Begin();

			MotionEditor motionEditor = GameObject.FindObjectOfType<MotionEditor>();

			Utility.SetGUIColor(UltiDraw.LightGrey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();

				Utility.SetGUIColor(UltiDraw.Orange);
				using(new EditorGUILayout.VerticalScope ("Box")) {
					Utility.ResetGUIColor();
					EditorGUILayout.LabelField(this == Module.GetRegularPhaseFunction() ? "Regular" : "Inverse");
				}

				MotionData.Frame frame = Module.Data.GetFrame(motionEditor.GetState().Index);

				if(IsKey(frame)) {
					SetPhase(frame, EditorGUILayout.Slider("Phase", GetPhase(frame), 0f, 1f));
				} else {
					EditorGUI.BeginDisabledGroup(true);
					SetPhase(frame, EditorGUILayout.Slider("Phase", GetPhase(frame), 0f, 1f));
					EditorGUI.EndDisabledGroup();
				}

				if(IsKey(frame)) {
					if(Utility.GUIButton("Unset Key", UltiDraw.Grey, UltiDraw.White)) {
						SetKey(frame, false);
						Save();
					}
				} else {
					if(Utility.GUIButton("Set Key", UltiDraw.DarkGrey, UltiDraw.White)) {
						SetKey(frame, true);
						Save();
					}
				}
				
				EditorGUILayout.BeginHorizontal();
				if(Utility.GUIButton("<", UltiDraw.DarkGrey, UltiDraw.White, 25f, 50f)) {
					motionEditor.LoadFrame((GetPreviousKey(frame).Timestamp));
				}

				EditorGUILayout.BeginVertical(GUILayout.Height(50f));
				Rect ctrl = EditorGUILayout.GetControlRect();
				Rect rect = new Rect(ctrl.x, ctrl.y, ctrl.width, 50f);
				EditorGUI.DrawRect(rect, UltiDraw.Black);

				float startTime = frame.Timestamp-Module.TimeWindow/2f;
				float endTime = frame.Timestamp+Module.TimeWindow/2f;
				if(startTime < 0f) {
					endTime -= startTime;
					startTime = 0f;
				}
				if(endTime > Module.Data.GetTotalTime()) {
					startTime -= endTime-Module.Data.GetTotalTime();
					endTime = Module.Data.GetTotalTime();
				}
				startTime = Mathf.Max(0f, startTime);
				endTime = Mathf.Min(Module.Data.GetTotalTime(), endTime);
				int start = Module.Data.GetFrame(startTime).Index;
				int end = Module.Data.GetFrame(endTime).Index;
				int elements = end-start;

				Vector3 prevPos = Vector3.zero;
				Vector3 newPos = Vector3.zero;
				Vector3 bottom = new Vector3(0f, rect.yMax, 0f);
				Vector3 top = new Vector3(0f, rect.yMax - rect.height, 0f);

				if(Module.ShowVelocities) {
					//Regular Velocities
					for(int i=1; i<elements; i++) {
						prevPos.x = rect.xMin + (float)(i-1)/(elements-1) * rect.width;
						prevPos.y = rect.yMax - RegularNormalisedVelocities[i+start-1] * rect.height;
						newPos.x = rect.xMin + (float)(i)/(elements-1) * rect.width;
						newPos.y = rect.yMax - RegularNormalisedVelocities[i+start] * rect.height;
						UltiDraw.DrawLine(prevPos, newPos, this == Module.GetRegularPhaseFunction() ? UltiDraw.Green : UltiDraw.Red);
					}

					//Inverse Velocities
					for(int i=1; i<elements; i++) {
						prevPos.x = rect.xMin + (float)(i-1)/(elements-1) * rect.width;
						prevPos.y = rect.yMax - InverseNormalisedVelocities[i+start-1] * rect.height;
						newPos.x = rect.xMin + (float)(i)/(elements-1) * rect.width;
						newPos.y = rect.yMax - InverseNormalisedVelocities[i+start] * rect.height;
						UltiDraw.DrawLine(prevPos, newPos, this == Module.GetRegularPhaseFunction() ? UltiDraw.Red : UltiDraw.Green);
					}
				}
				
				if(Module.ShowCycle) {
					//Cycle
					for(int i=1; i<elements; i++) {
						prevPos.x = rect.xMin + (float)(i-1)/(elements-1) * rect.width;
						prevPos.y = rect.yMax - NormalisedCycle[i+start-1] * rect.height;
						newPos.x = rect.xMin + (float)(i)/(elements-1) * rect.width;
						newPos.y = rect.yMax - NormalisedCycle[i+start] * rect.height;
						UltiDraw.DrawLine(prevPos, newPos, UltiDraw.Yellow);
					}
				}

				//Phase
				//for(int i=1; i<Module.Data.Frames.Length; i++) {
				//	MotionData.Frame A = Module.Data.Frames[i-1];
				//	MotionData.Frame B = Module.Data.Frames[i];
				//	prevPos.x = rect.xMin + (float)(A.Index-start)/elements * rect.width;
				//	prevPos.y = rect.yMax - Mathf.Repeat(Phase[A.Index-1], 1f) * rect.height;
				//	newPos.x = rect.xMin + (float)(B.Index-start)/elements * rect.width;
				//	newPos.y = rect.yMax - Phase[B.Index-1] * rect.height;
				//	UltiDraw.DrawLine(prevPos, newPos, UltiDraw.White);
				//	bottom.x = rect.xMin + (float)(B.Index-start)/elements * rect.width;
				//	top.x = rect.xMin + (float)(B.Index-start)/elements * rect.width;
				//}
				
				MotionData.Frame A = Module.Data.GetFrame(start);
				if(A.Index == 1) {
					bottom.x = rect.xMin;
					top.x = rect.xMin;
					UltiDraw.DrawLine(bottom, top, UltiDraw.Magenta.Transparent(0.5f));
				}
				MotionData.Frame B = GetNextKey(A);
				while(A != B) {
					prevPos.x = rect.xMin + (float)(A.Index-start)/elements * rect.width;
					prevPos.y = rect.yMax - Mathf.Repeat(Phase[A.Index-1], 1f) * rect.height;
					newPos.x = rect.xMin + (float)(B.Index-start)/elements * rect.width;
					newPos.y = rect.yMax - Phase[B.Index-1] * rect.height;
					UltiDraw.DrawLine(prevPos, newPos, UltiDraw.White);
					bottom.x = rect.xMin + (float)(B.Index-start)/elements * rect.width;
					top.x = rect.xMin + (float)(B.Index-start)/elements * rect.width;
					UltiDraw.DrawLine(bottom, top, UltiDraw.Magenta.Transparent(0.5f));
					A = B;
					B = GetNextKey(A);
					if(B.Index > end) {
						break;
					}
				}

				//Seconds
				float timestamp = startTime;
				while(timestamp <= endTime) {
					float floor = Mathf.FloorToInt(timestamp);
					if(floor >= startTime && floor <= endTime) {
						top.x = rect.xMin + (float)(Module.Data.GetFrame(floor).Index-start)/elements * rect.width;
						UltiDraw.DrawCircle(top, 5f, UltiDraw.White);
					}
					timestamp += 1f;
				}
				//

				//Current Pivot
				top.x = rect.xMin + (float)(frame.Index-start)/elements * rect.width;
				bottom.x = rect.xMin + (float)(frame.Index-start)/elements * rect.width;
				UltiDraw.DrawLine(top, bottom, UltiDraw.Yellow);
				UltiDraw.DrawCircle(top, 3f, UltiDraw.Green);
				UltiDraw.DrawCircle(bottom, 3f, UltiDraw.Green);

				Handles.DrawLine(Vector3.zero, Vector3.zero); //Somehow needed to get it working...
				EditorGUILayout.EndVertical();

				if(Utility.GUIButton(">", UltiDraw.DarkGrey, UltiDraw.White, 25f, 50f)) {
					motionEditor.LoadFrame(GetNextKey(frame).Timestamp);
				}
				EditorGUILayout.EndHorizontal();
			}

			UltiDraw.End();
		}
	}

	public class PhaseEvolution {
		public static float AMPLITUDE = 10f;
		public static float FREQUENCY = 2.5f;
		public static float SHIFT = Mathf.PI;
		public static float OFFSET = 10f;
		public static float SLOPE = 5f;
		public static float WINDOW = 5f;
		
		public PhaseFunction Function;

		public Population[] Populations;

		public float[] LowerBounds;
		public float[] UpperBounds;

		public float Amplitude = AMPLITUDE;
		public float Frequency = FREQUENCY;
		public float Shift = SHIFT;
		public float Offset = OFFSET;
		public float Slope = SLOPE;

		public float Behaviour = 1f;

		public float Window = 1f;
		public float Blending = 1f;

		public PhaseEvolution(PhaseFunction function) {
			Function = function;

			LowerBounds = new float[5];
			UpperBounds = new float[5];

			SetAmplitude(Amplitude);
			SetFrequency(Frequency);
			SetShift(Shift);
			SetOffset(Offset);
			SetSlope(Slope);

			Initialise();
		}

		public void SetAmplitude(float value) {
			Amplitude = value;
			LowerBounds[0] = -value;
			UpperBounds[0] = value;
		}

		public void SetFrequency(float value) {
			Frequency = value;
			LowerBounds[1] = 0f;
			UpperBounds[1] = value;
		}

		public void SetShift(float value) {
			Shift = value;
			LowerBounds[2] = -value;
			UpperBounds[2] = value;
		}

		public void SetOffset(float value) {
			Offset = value;
			LowerBounds[3] = -value;
			UpperBounds[3] = value;
		}

		public void SetSlope(float value) {
			Slope = value;
			LowerBounds[4] = -value;
			UpperBounds[4] = value;
		}

		public void SetWindow(float value) {
			if(Window != value) {
				Window = value;
				Initialise();
			}
		}

		public void Initialise() {
			Interval[] intervals = new Interval[Mathf.FloorToInt(Function.Module.Data.GetTotalTime() / Window) + 1];
			for(int i=0; i<intervals.Length; i++) {
				int start = Function.Module.Data.GetFrame(i*Window).Index-1;
				int end = Function.Module.Data.GetFrame(Mathf.Min(Function.Module.Data.GetTotalTime(), (i+1)*Window)).Index-2;
				if(end == Function.Module.Data.GetTotalFrames()-2) {
					end += 1;
				}
				intervals[i] = new Interval(start, end);
			}
			Populations = new Population[intervals.Length];
			for(int i=0; i<Populations.Length; i++) {
				Populations[i] = new Population(this, 50, 5, intervals[i]);
			}
		}

		public void Optimise() {
			for(int i=0; i<Populations.Length; i++) {
				Populations[i].Active = IsActive(i);
			}
			for(int i=0; i<Populations.Length; i++) {
				Populations[i].Evolve(GetPreviousPopulation(i), GetNextPopulation(i), GetPreviousPivotPopulation(i), GetNextPivotPopulation(i));
			}
			Assign();
		}
		
		public void Assign() {
			for(int i=0; i<Function.Module.Data.GetTotalFrames(); i++) {
				Function.Keys[i] = false;
				Function.Phase[i] = 0f;
				Function.Cycle[i] = 0f;
				Function.NormalisedCycle[i] = 0f;
			}

			//Compute cycle
			float min = float.MaxValue;
			float max = float.MinValue;
			for(int i=0; i<Populations.Length; i++) {
				for(int j=Populations[i].Interval.Start; j<=Populations[i].Interval.End; j++) {
					Function.Cycle[j] = Interpolate(i, j);
					min = Mathf.Min(min, Function.Cycle[j]);
					max = Mathf.Max(max, Function.Cycle[j]);
				}
			}
			for(int i=0; i<Populations.Length; i++) {
				for(int j=Populations[i].Interval.Start; j<=Populations[i].Interval.End; j++) {
					Function.NormalisedCycle[j] = Utility.Normalise(Function.Cycle[j], min, max, 0f, 1f);
				}
			}

			//Fill with frequency negative turning points
			for(int i=0; i<Populations.Length; i++) {
				for(int j=Populations[i].Interval.Start; j<=Populations[i].Interval.End; j++) {
					if(InterpolateD2(i, j) <= 0f && InterpolateD2(i, j+1) >= 0f) {
						Function.Keys[j] = true;
					}
				}
			}

			//Compute phase
			for(int i=0; i<Function.Keys.Length; i++) {
				if(Function.Keys[i]) {
					Function.SetPhase(Function.Module.Data.Frames[i], i == 0 ? 0f : 1f);
				}
			}
		}

		public Population GetPreviousPopulation(int current) {
			return Populations[Mathf.Max(0, current-1)];
		}

		public Population GetPreviousPivotPopulation(int current) {
			for(int i=current-1; i>=0; i--) {
				if(Populations[i].Active) {
					return Populations[i];
				}
			}
			return Populations[0];
		}

		public Population GetNextPopulation(int current) {
			return Populations[Mathf.Min(Populations.Length-1, current+1)];
		}

		public Population GetNextPivotPopulation(int current) {
			for(int i=current+1; i<Populations.Length; i++) {
				if(Populations[i].Active) {
					return Populations[i];
				}
			}
			return Populations[Populations.Length-1];
		}

		public bool IsActive(int interval) {
			float velocity = 0f;
			for(int i=Populations[interval].Interval.Start; i<=Populations[interval].Interval.End; i++) {
				velocity += Function.RegularVelocities[i] + Function.InverseVelocities[i];
			}
			return velocity / Populations[interval].Interval.Length > 0f;
		}

		public float Interpolate(int interval, int frame) {
			interval = Mathf.Clamp(interval, 0, Populations.Length-1);
			Population current = Populations[interval];
			float value = current.Phenotype(current.GetWinner().Genes, frame);
			float pivot = (float)(frame-current.Interval.Start) / (float)(current.Interval.Length-1) - 0.5f;
			float threshold = 0.5f * (1f - Blending);
			if(pivot < -threshold) {
				Population previous = GetPreviousPopulation(interval);
				float blend = 0.5f * (pivot + threshold) / (-0.5f + threshold);
				float prevValue = previous.Phenotype(previous.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * prevValue;
			}
			if(pivot > threshold) {
				Population next = GetNextPopulation(interval);
				float blend = 0.5f * (pivot - threshold) / (0.5f - threshold);
				float nextValue = next.Phenotype(next.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * nextValue;
			}
			return value;
		}

		public float InterpolateD1(int interval, int frame) {
			interval = Mathf.Clamp(interval, 0, Populations.Length-1);
			Population current = Populations[interval];
			float value = current.Phenotype1(current.GetWinner().Genes, frame);
			float pivot = (float)(frame-current.Interval.Start) / (float)(current.Interval.Length-1) - 0.5f;
			float threshold = 0.5f * (1f - Blending);
			if(pivot < -threshold) {
				Population previous = GetPreviousPopulation(interval);
				float blend = 0.5f * (pivot + threshold) / (-0.5f + threshold);
				float prevValue = previous.Phenotype1(previous.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * prevValue;
			}
			if(pivot > threshold) {
				Population next = GetNextPopulation(interval);
				float blend = 0.5f * (pivot - threshold) / (0.5f - threshold);
				float nextValue = next.Phenotype1(next.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * nextValue;
			}
			return value;
		}

		public float InterpolateD2(int interval, int frame) {
			interval = Mathf.Clamp(interval, 0, Populations.Length-1);
			Population current = Populations[interval];
			float value = current.Phenotype2(current.GetWinner().Genes, frame);
			float pivot = (float)(frame-current.Interval.Start) / (float)(current.Interval.Length-1) - 0.5f;
			float threshold = 0.5f * (1f - Blending);
			if(pivot < -threshold) {
				Population previous = GetPreviousPopulation(interval);
				float blend = 0.5f * (pivot + threshold) / (-0.5f + threshold);
				float prevValue = previous.Phenotype2(previous.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * prevValue;
			}
			if(pivot > threshold) {
				Population next = GetNextPopulation(interval);
				float blend = 0.5f * (pivot - threshold) / (0.5f - threshold);
				float nextValue = next.Phenotype2(next.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * nextValue;
			}
			return value;
		}

		public float InterpolateD3(int interval, int frame) {
			interval = Mathf.Clamp(interval, 0, Populations.Length-1);
			Population current = Populations[interval];
			float value = current.Phenotype3(current.GetWinner().Genes, frame);
			float pivot = (float)(frame-current.Interval.Start) / (float)(current.Interval.Length-1) - 0.5f;
			float threshold = 0.5f * (1f - Blending);
			if(pivot < -threshold) {
				Population previous = GetPreviousPopulation(interval);
				float blend = 0.5f * (pivot + threshold) / (-0.5f + threshold);
				float prevValue = previous.Phenotype3(previous.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * prevValue;
			}
			if(pivot > threshold) {
				Population next = GetNextPopulation(interval);
				float blend = 0.5f * (pivot - threshold) / (0.5f - threshold);
				float nextValue = next.Phenotype3(next.GetWinner().Genes, frame);
				value = (1f-blend) * value + blend * nextValue;
			}
			return value;
		}

		public float GetFitness() {
			float fitness = 0f;
			for(int i=0; i<Populations.Length; i++) {
				fitness += Populations[i].GetFitness();
			}
			return fitness / Populations.Length;
		}

		public float[] GetPeakConfiguration() {
			float[] configuration = new float[5];
			for(int i=0; i<5; i++) {
				configuration[i] = float.MinValue;
			}
			for(int i=0; i<Populations.Length; i++) {
				for(int j=0; j<5; j++) {
					configuration[j] = Mathf.Max(configuration[j], Mathf.Abs(Populations[i].GetWinner().Genes[j]));
				}
			}
			return configuration;
		}

		public class Population {
			public PhaseEvolution Evolution;
			public int Size;
			public int Dimensionality;
			public Interval Interval;

			public bool Active;

			public Individual[] Individuals;
			public Individual[] Offspring;
			public float[] RankProbabilities;
			public float RankProbabilitySum;

			public Population(PhaseEvolution evolution, int size, int dimensionality, Interval interval) {
				Evolution = evolution;
				Size = size;
				Dimensionality = dimensionality;
				Interval = interval;

				//Create individuals
				Individuals = new Individual[Size];
				Offspring = new Individual[Size];
				for(int i=0; i<Size; i++) {
					Individuals[i] = new Individual(Dimensionality);
					Offspring[i] = new Individual(Dimensionality);
				}

				//Compute rank probabilities
				RankProbabilities = new float[Size];
				float rankSum = (float)(Size*(Size+1)) / 2f;
				for(int i=0; i<Size; i++) {
					RankProbabilities[i] = (float)(Size-i)/(float)rankSum;
				}
				for(int i=0; i<Size; i++) {
					RankProbabilitySum += RankProbabilities[i];
				}

				//Initialise randomly
				for(int i=0; i<Size; i++) {
					Reroll(Individuals[i]);
				}

				//Evaluate fitness
				for(int i=0; i<Size; i++) {
					Individuals[i].Fitness = ComputeFitness(Individuals[i].Genes);
				}

				//Sort
				SortByFitness(Individuals);

				//Evaluate extinctions
				AssignExtinctions(Individuals);
			}

			public void Evolve(Population previous, Population next, Population previousPivot, Population nextPivot) {
				if(Active) {
					//Copy elite
					Copy(Individuals[0], Offspring[0]);

					//Memetic exploitation
					Exploit(Offspring[0]);

					//Remaining individuals
					for(int o=1; o<Size; o++) {
						Individual offspring = Offspring[o];
						if(Random.value <= Evolution.Behaviour) {
							Individual parentA = Select(Individuals);
							Individual parentB = Select(Individuals);
							while(parentB == parentA) {
								parentB = Select(Individuals);
							}
							Individual prototype = Select(Individuals);
							while(prototype == parentA || prototype == parentB) {
								prototype = Select(Individuals);
							}

							float mutationRate = GetMutationProbability(parentA, parentB);
							float mutationStrength = GetMutationStrength(parentA, parentB);

							for(int i=0; i<Dimensionality; i++) {
								float weight;

								//Recombination
								weight = Random.value;
								float momentum = Random.value * parentA.Momentum[i] + Random.value * parentB.Momentum[i];
								if(Random.value < 0.5f) {
									offspring.Genes[i] = parentA.Genes[i] + momentum;
								} else {
									offspring.Genes[i] = parentB.Genes[i] + momentum;
								}

								//Store
								float gene = offspring.Genes[i];

								//Mutation
								if(Random.value <= mutationRate) {
									float span = Evolution.UpperBounds[i] - Evolution.LowerBounds[i];
									offspring.Genes[i] += Random.Range(-mutationStrength*span, mutationStrength*span);
								}
								
								//Adoption
								weight = Random.value;
								offspring.Genes[i] += 
									weight * Random.value * (0.5f * (parentA.Genes[i] + parentB.Genes[i]) - offspring.Genes[i])
									+ (1f-weight) * Random.value * (prototype.Genes[i] - offspring.Genes[i]);

								//Constrain
								offspring.Genes[i] = Mathf.Clamp(offspring.Genes[i], Evolution.LowerBounds[i], Evolution.UpperBounds[i]);

								//Momentum
								offspring.Momentum[i] = Random.value * momentum + (offspring.Genes[i] - gene);
							}
						} else {
							Reroll(offspring);
						}
					}

					//Evaluate fitness
					for(int i=0; i<Size; i++) {
						Offspring[i].Fitness = ComputeFitness(Offspring[i].Genes);
					}

					//Sort
					SortByFitness(Offspring);

					//Evaluate extinctions
					AssignExtinctions(Offspring);

					//Form new population
					for(int i=0; i<Size; i++) {
						Copy(Offspring[i], Individuals[i]);
					}
				} else {
					//Postprocess
					for(int i=0; i<Size; i++) {
						Individuals[i].Genes[0] = 1f;
						Individuals[i].Genes[1] = 1f;
						Individuals[i].Genes[2] = 0.5f * (previousPivot.GetWinner().Genes[2] + nextPivot.GetWinner().Genes[2]);
						Individuals[i].Genes[3] = 0.5f * (previousPivot.GetWinner().Genes[3] + nextPivot.GetWinner().Genes[3]);
						Individuals[i].Genes[4] = 0f;
						for(int j=0; j<5; j++) {
							Individuals[i].Momentum[j] = 0f;
						}
						Individuals[i].Fitness = 0f;
						Individuals[i].Extinction = 0f;
					}
				}
			}

			//Returns the mutation probability from two parents
			private float GetMutationProbability(Individual parentA, Individual parentB) {
				float extinction = 0.5f * (parentA.Extinction + parentB.Extinction);
				float inverse = 1f/(float)Dimensionality;
				return extinction * (1f-inverse) + inverse;
			}

			//Returns the mutation strength from two parents
			private float GetMutationStrength(Individual parentA, Individual parentB) {
				return 0.5f * (parentA.Extinction + parentB.Extinction);
			}

			public Individual GetWinner() {
				return Individuals[0];
			}

			public float GetFitness() {
				return GetWinner().Fitness;
			}

			private void Copy(Individual from, Individual to) {
				for(int i=0; i<Dimensionality; i++) {
					to.Genes[i] = Mathf.Clamp(from.Genes[i], Evolution.LowerBounds[i], Evolution.UpperBounds[i]);
					to.Momentum[i] = from.Momentum[i];
				}
				to.Extinction = from.Extinction;
				to.Fitness = from.Fitness;
			}

			private void Reroll(Individual individual) {
				for(int i=0; i<Dimensionality; i++) {
					individual.Genes[i] = Random.Range(Evolution.LowerBounds[i], Evolution.UpperBounds[i]);
				}
			}

			private void Exploit(Individual individual) {
				individual.Fitness = ComputeFitness(individual.Genes);
				for(int i=0; i<Dimensionality; i++) {
					float gene = individual.Genes[i];

					float span = Evolution.UpperBounds[i] - Evolution.LowerBounds[i];

					float incGene = Mathf.Clamp(gene + Random.value*individual.Fitness*span, Evolution.LowerBounds[i], Evolution.UpperBounds[i]);
					individual.Genes[i] = incGene;
					float incFitness = ComputeFitness(individual.Genes);

					float decGene = Mathf.Clamp(gene - Random.value*individual.Fitness*span, Evolution.LowerBounds[i], Evolution.UpperBounds[i]);
					individual.Genes[i] = decGene;
					float decFitness = ComputeFitness(individual.Genes);

					individual.Genes[i] = gene;

					if(incFitness < individual.Fitness) {
						individual.Genes[i] = incGene;
						individual.Momentum[i] = incGene - gene;
						individual.Fitness = incFitness;
					}

					if(decFitness < individual.Fitness) {
						individual.Genes[i] = decGene;
						individual.Momentum[i] = decGene - gene;
						individual.Fitness = decFitness;
					}
				}
			}

			//Rank-based selection of an individual
			private Individual Select(Individual[] pool) {
				double rVal = Random.value * RankProbabilitySum;
				for(int i=0; i<Size; i++) {
					rVal -= RankProbabilities[i];
					if(rVal <= 0.0) {
						return pool[i];
					}
				}
				return pool[Size-1];
			}

			//Sorts all individuals starting with best (lowest) fitness
			private void SortByFitness(Individual[] individuals) {
				System.Array.Sort(individuals,
					delegate(Individual a, Individual b) {
						return a.Fitness.CompareTo(b.Fitness);
					}
				);
			}

			//Multi-Objective RMSE
			private float ComputeFitness(float[] genes) {
				float fitness = 0f;
				for(int i=Interval.Start; i<=Interval.End; i++) {
					float y1 = Evolution.Function == Evolution.Function.Module.GetRegularPhaseFunction() ? Evolution.Function.RegularVelocities[i] : Evolution.Function.InverseVelocities[i];
					float y2 = Evolution.Function == Evolution.Function.Module.GetRegularPhaseFunction() ? Evolution.Function.InverseVelocities[i] : Evolution.Function.RegularVelocities[i];
					float x = Phenotype(genes, i);
					float error = (y1-x)*(y1-x) + (-y2-x)*(-y2-x);
					float sqrError = error*error;
					fitness += sqrError;
				}
				fitness /= Interval.Length;
				fitness = Mathf.Sqrt(fitness);
				return fitness;
			}
			
			public float Phenotype(float[] genes, int frame) {
				return Utility.LinSin(
					genes[0], 
					genes[1], 
					genes[2], 
					genes[3] - (float)(frame-Interval.Start)*genes[4]/Evolution.Function.Module.Data.Framerate, 
					genes[4], 
					frame/Evolution.Function.Module.Data.Framerate
					);
			}

			public float Phenotype1(float[] genes, int frame) {
				return Utility.LinSin1(
					genes[0], 
					genes[1], 
					genes[2], 
					genes[3] - (float)(frame-Interval.Start)*genes[4]/Evolution.Function.Module.Data.Framerate,
					genes[4], 
					frame/Evolution.Function.Module.Data.Framerate
					);
			}

			public float Phenotype2(float[] genes, int frame) {
				return Utility.LinSin2(
					genes[0], 
					genes[1], 
					genes[2], 
					genes[3] - (float)(frame-Interval.Start)*genes[4]/Evolution.Function.Module.Data.Framerate,
					genes[4], 
					frame/Evolution.Function.Module.Data.Framerate
					);
			}

			public float Phenotype3(float[] genes, int frame) {
				return Utility.LinSin3(
					genes[0], 
					genes[1], 
					genes[2], 
					genes[3] - (float)(frame-Interval.Start)*genes[4]/Evolution.Function.Module.Data.Framerate,
					genes[4], 
					frame/Evolution.Function.Module.Data.Framerate
					);
			}

			//Compute extinction values
			private void AssignExtinctions(Individual[] individuals) {
				float min = individuals[0].Fitness;
				float max = individuals[Size-1].Fitness;
				for(int i=0; i<Size; i++) {
					float grading = (float)i/((float)Size-1);
					individuals[i].Extinction = (individuals[i].Fitness + min*(grading-1f)) / max;
				}
			}
		}

		public class Individual {
			public float[] Genes;
			public float[] Momentum;
			public float Extinction;
			public float Fitness;
			public Individual(int dimensionality) {
				Genes = new float[dimensionality];
				Momentum = new float[dimensionality];
			}
		}

		public class Interval {
			public int Start;
			public int End;
			public int Length;
			public Interval(int start, int end) {
				Start = start;
				End = end;
				Length = end-start+1;
			}
		}

	}

	[CustomEditor(typeof(PhaseEditor))]
	public class PhaseEditor_Editor : Editor {

		public PhaseEditor Target;

		private float RefreshRate = 30f;
		private System.DateTime Timestamp;

		void Awake() {
			Target = (PhaseEditor)target;
			Target.Refresh();
			Timestamp = Utility.GetTimestamp();
			EditorApplication.update += EditorUpdate;
		}

		void OnDestroy() {
			EditorApplication.update -= EditorUpdate;
		}

		public void EditorUpdate() {
			if(Utility.GetElapsedTime(Timestamp) >= 1f/RefreshRate) {
				Repaint();
				Timestamp = Utility.GetTimestamp();
			}
		}

		public override void OnInspectorGUI() {
			Undo.RecordObject(Target, Target.name);
			Inspector();
			if(GUI.changed) {
				EditorUtility.SetDirty(Target);
			}
		}

		public void Inspector() {
			if(Utility.GUIButton("Reset", UltiDraw.DarkGrey, UltiDraw.White)) {
				Target.Modules = new PhaseModule[0];
				Target.Refresh();
			}
			EditorGUILayout.LabelField("Modules: " + Target.Modules.Length);
			PhaseModule module = Target.GetModule();
			if(module == null) {
				return;
			}
			EditorGUILayout.LabelField("Module: " + module.Data.Name);
			module.TimeWindow = EditorGUILayout.Slider("Time Window", module.TimeWindow, 0f, module.Data.GetTotalTime());
			module.SetMaximumVelocity(EditorGUILayout.FloatField("Maximum Velocity", module.MaximumVelocity));
			module.SetVelocityThreshold(EditorGUILayout.FloatField("Velocity Threshold", module.VelocityThreshold));
			string[] names = new string[1 + Target.GetEditor().GetData().Source.Bones.Length];
			names[0] = "Select...";
			for(int i=0; i<names.Length-1; i++) {
				names[i+1] = Target.GetEditor().GetData().Source.Bones[i].Name;
			}
			int index = EditorGUILayout.Popup("Phase Detector", 0, names);
			if(index > 0) {
				module.ToggleVariable(index-1);
			}
			for(int i=0; i<module.Data.Source.Bones.Length; i++) {
				if(module.Variables[i]) {
					EditorGUILayout.LabelField(module.Data.Source.Bones[i].Name + " <-> " + module.Data.Source.Bones[module.Data.Symmetry[i]].Name);
				}
			}
			module.ShowVelocities = EditorGUILayout.Toggle("Show Velocities", module.ShowVelocities);
			module.ShowCycle = EditorGUILayout.Toggle("Show Cycle", module.ShowCycle);
			Utility.SetGUIColor(UltiDraw.Grey);
			using(new EditorGUILayout.VerticalScope ("Box")) {
				Utility.ResetGUIColor();
				if(module.Optimising) {
					if(Utility.GUIButton("Stop Optimisation", UltiDraw.LightGrey, UltiDraw.Black)) {
						module.Optimising = !module.Optimising;
						module.GetRegularPhaseFunction().Save();
						module.GetInversePhaseFunction().Save();
					}
				} else {
					if(Utility.GUIButton("Start Optimisation", UltiDraw.DarkGrey, UltiDraw.White)) {
						module.Optimising = !module.Optimising;
					}
				}
				if(Utility.GUIButton("Clear", UltiDraw.DarkGrey, UltiDraw.White)) {
					module.GetRegularPhaseFunction().Clear();
					module.GetInversePhaseFunction().Clear();
					module.GetRegularPhaseFunction().Save();
					module.GetInversePhaseFunction().Save();
				}
			}
			module.GetRegularPhaseFunction().Inspector();
			module.GetInversePhaseFunction().Inspector();
			module.GetRegularPhaseFunction().EditorUpdate();
			module.GetInversePhaseFunction().EditorUpdate();

		}
	}
}
#endif