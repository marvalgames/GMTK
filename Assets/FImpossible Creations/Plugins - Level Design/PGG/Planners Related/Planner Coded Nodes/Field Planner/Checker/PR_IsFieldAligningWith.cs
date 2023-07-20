using FIMSpace.Generating.Checker;
using FIMSpace.Graph;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.PlannerNodes.Field.Checker
{

    public class PR_IsFieldAligningWith : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "  Is Aligning With" : "Is Field Aligning With"; }
        public override string GetNodeTooltipDescription { get { return "Check if field aligns with another - not separated but just next to it or overlapping.\nCan be executed but bool port also triggers calculations."; } }
        public override Color GetNodeColor() { return new Color(0.07f, 0.66f, 0.56f, 0.9f); }
        public override Vector2 NodeSize { get { return new Vector2(222, _EditorFoldout ? 126 : 102); } }
        public override bool IsFoldable { get { return true; } }
        public override bool DrawInputConnector { get { return true; } }
        public override bool DrawOutputConnector { get { return false; } }


        [Port(EPortPinType.Input, 1)] public PGGPlannerPort AligningWith;
        [Port(EPortPinType.Output, EPortValueDisplay.NotEditable, 1)][Tooltip("If collision occured then true, if no then false")] public BoolPort IsAligning;
        [HideInInspector][Port(EPortPinType.Input, 1)][Tooltip("Using self if no input")] public PGGPlannerPort FirstField;

        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.WholeFieldPlacement; } }

        public override void OnStartReadingNode()
        {
            //UnityEngine.Debug.Log("inputconn = " + InputConnections.Count);
            if (InputConnections.Count > 0) return;
            ProceedAlignCheck();
        }

        public override void Execute(PlanGenerationPrint print, PlannerResult newResult)
        {
            ProceedAlignCheck();
        }

        void ProceedAlignCheck()
        {
            IsAligning.Value = false;

            FieldPlanner myPlanner = GetPlannerFromPort(FirstField);
            if (myPlanner == null) return;

            FieldPlanner bPlanner = GetPlannerFromPort(AligningWith);
            if (bPlanner == null) return;

            if (bPlanner == CurrentExecutingPlanner /*|| bPlanner == newResult.ParentFieldPlanner*/) return; // Dont check with self

            IsAligning.Value = false;

            bool alignDetect = myPlanner.LatestChecker.IsAnyAligning(bPlanner.LatestChecker);

            IsAligning.Value = alignDetect;

            //if (Debugging)
            //{
            //    if (alignDetect)
            //    {
            //        DebuggingInfo = "Checking aligning and detected with " + bPlanner.name + " " + bPlanner.ArrayNameString;
            //    }
            //    else
            //    {
            //        DebuggingInfo = "Checking aligning but no contact detected";
            //    }

            //    print._debugLatestExecuted = myPlanner.LatestResult.Checker;
            //}
        }


#if UNITY_EDITOR
        SerializedProperty sp = null;
        public override void Editor_OnNodeBodyGUI(ScriptableObject setup)
        {
            base.Editor_OnNodeBodyGUI(setup);
            AligningWith.Editor_DefaultValueInfo = "(None)";

            if (!_EditorFoldout) return;

            if (_EditorFoldout)
            {
                FirstField.AllowDragWire = true;
                baseSerializedObject.Update();
                if (sp == null) sp = baseSerializedObject.FindProperty("FirstField");
                SerializedProperty spc = sp.Copy();
                EditorGUILayout.PropertyField(spc);
                baseSerializedObject.ApplyModifiedProperties();
            }
            else
            {
                FirstField.AllowDragWire = false;
            }

        }
#endif

    }
}