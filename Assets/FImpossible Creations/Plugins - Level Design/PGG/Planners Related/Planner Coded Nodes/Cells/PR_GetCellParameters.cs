using FIMSpace.Graph;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.PlannerNodes.Cells
{

    public class PR_GetCellParameters : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "Cell Parameters" : "Get Cell Parameters"; }
        public override string GetNodeTooltipDescription { get { return "Accessing some parameters of provided cell"; } }
        public override Color GetNodeColor() { return new Color(0.64f, 0.9f, 0.0f, 0.9f); }
        public override Vector2 NodeSize { get { return new Vector2(CellParameter == ECellParameter.InternalCellDirection ? 230 : 190, 100); } }
        public override bool DrawInputConnector { get { return false; } }
        public override bool DrawOutputConnector { get { return false; } }
        public override bool IsFoldable { get { return false; } }

        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.ReadData; } }

        public enum ECellParameter
        {
            InFieldLocalPos, WorldPosition, Owner, InternalCellDirection, CellData, CellInstruction
        }

        [HideInInspector][Port(EPortPinType.Input)] public PGGCellPort Cell;
        [HideInInspector] public ECellParameter CellParameter = ECellParameter.WorldPosition;

        [HideInInspector][Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.HideValue)] public PGGVector3Port Output;
        [HideInInspector][Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.HideValue)] public PGGPlannerPort Owner;
        [HideInInspector][Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.HideValue)] public PGGUniversalPort OtherParameter;
        Vector3 read = Vector3.zero;

        public override void OnStartReadingNode()
        {
            Cell.TriggerReadPort(true);
            Output.Value = Vector3.zero;
            Owner.Clear();
            OtherParameter.Variable.SetTemporaryReference(true);

            if ( CellParameter == ECellParameter.Owner)
            {
                FieldPlanner cellPlanner = Cell.GetInputPlannerIfPossible;
                //PlannerResult res = Cell.GetInputResultValue;
                //if (res == null) return;
                if (cellPlanner == null) return;
                Owner.SetIDsOfPlanner(cellPlanner);
            }
            else if ( CellParameter == ECellParameter.CellInstruction)
            {
                var cell = Cell.GetInputCellValue;
                if (cell == null) return;
                FieldPlanner cellPlanner = Cell.GetInputPlannerIfPossible;
                if (cellPlanner == null) return;

                SpawnInstructionGuide instrGuide = null;
                for (int i = 0; i < cellPlanner.LatestResult.CellsInstructions.Count; i++)
                {
                    if (cellPlanner.LatestResult.CellsInstructions[i].pos == cell.Pos)
                    {
                        instrGuide = cellPlanner.LatestResult.CellsInstructions[i];
                        break;
                    }
                }

                if ( instrGuide != null) OtherParameter.Variable.SetTemporaryReference(true, instrGuide);
            }
            else if ( CellParameter == ECellParameter.CellData)
            {
                var cell = Cell.GetInputCellValue;
                if (cell == null) return;

                if ( cell.GetCustomDatasCount() > 0)
                {
                    string val = cell.cellCustomData[0];
                    for (int d = 1; d < cell.cellCustomData.Count; d++) val += "," + cell.cellCustomData[d];
                    OtherParameter.Variable.SetTemporaryReference(false, null);
                    OtherParameter.Variable.SetValue(val);
                }
                else
                {
                    OtherParameter.Variable.SetTemporaryReference(true, null);
                }
            }
            else
            {
                var cell = Cell.GetInputCellValue;
                if (cell == null) return;

                var checker = Cell.GetInputCheckerValue;

                Vector3 size = Vector3.one;
                if (checker != null) size = checker.RootScale;

                if (CellParameter == ECellParameter.InFieldLocalPos) Output.Value = cell.Pos;
                else if (CellParameter == ECellParameter.WorldPosition)
                {
                    if ( checker != null)
                    {
                        Output.Value = checker.GetWorldPos(cell);
                    }
                    else
                    {
                        Output.Value = cell.WorldPos(size);
                    }
                }
                else if (CellParameter == ECellParameter.InternalCellDirection) Output.Value = cell.HelperDirection;
            }
        }

#if UNITY_EDITOR

        private SerializedProperty sp = null;
        public override void Editor_OnNodeBodyGUI(ScriptableObject setup)
        {
            base.Editor_OnNodeBodyGUI(setup);

            baseSerializedObject.Update();
            if (sp == null) sp = baseSerializedObject.FindProperty("Cell");
            SerializedProperty spc = sp.Copy();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(spc, GUILayout.Width(50)); spc.Next(false);
            EditorGUILayout.PropertyField(spc, GUIContent.none); spc.Next(false);
            EditorGUILayout.EndHorizontal();

            Output.AllowDragWire = false;
            Owner.AllowDragWire = false;
            OtherParameter.AllowDragWire = false;

            if (CellParameter == ECellParameter.Owner)
            {
                spc.Next(false); EditorGUILayout.PropertyField(spc);
                Owner.AllowDragWire = true;
            }
            else if (CellParameter == ECellParameter.CellInstruction)
            {
                spc.Next(false);spc.Next(false); EditorGUILayout.PropertyField(spc);
                OtherParameter.AllowDragWire = true;
            }
            else if (CellParameter == ECellParameter.CellData)
            {
                spc.Next(false);spc.Next(false); EditorGUILayout.PropertyField(spc);
                OtherParameter.AllowDragWire = true;
            }
            else
            {
                Output.AllowDragWire = true;
                EditorGUILayout.PropertyField(spc); spc.Next(false);
            }


            baseSerializedObject.ApplyModifiedProperties();
        }


        public override void Editor_OnAdditionalInspectorGUI()
        {
            EditorGUILayout.LabelField("Debugging:", EditorStyles.helpBox);

            if (Cell.GetInputCellValue == null) EditorGUILayout.LabelField("NULL CELL!");
            else
            {
                EditorGUILayout.LabelField("Out Value: " + read);
            }
        }
#endif

    }
}