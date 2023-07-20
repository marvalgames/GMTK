using FIMSpace.Generating.Planning.PlannerNodes;
using FIMSpace.Graph;
using UnityEngine;
using FIMSpace.Generating.Rules;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.ModNodes.Cells
{

    public class MR_LineCheckCells : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "  Line Check Cells" : "Line Check Cells"; }
        public override string GetNodeTooltipDescription { get { return "Checking for cells state in the line in all 4 directions.\nUseful for generating object with scale-aligned to reach wall on the other side of the grid."; } }
        public override Color GetNodeColor() { return new Color(0.64f, 0.9f, 0.0f, 0.9f); }
        public override bool IsFoldable { get { return true; } }
        public override Vector2 NodeSize { get { return new Vector2(212, 140 + extraNodeHeight); } }
        int extraNodeHeight = 0;

        public override bool DrawInputConnector { get { return true; } }
        public override bool DrawOutputConnector { get { return true; } }

        public enum EPurpose
        {
            ChooseNearestDir, ChooseNearestDir_NoZero, ChooseFarthestDir
        }

        public EPurpose CheckResult = EPurpose.ChooseNearestDir_NoZero;

        [Port(EPortPinType.Output)] public PGGVector3Port Direction;
        [Port(EPortPinType.Output)] public IntPort CellsDistance;
        public ESR_Space StopAt = ESR_Space.OutOfGrid;

        [HideInInspector][Port(EPortPinType.Input)] public PGGStringPort Tag;
        [HideInInspector] public ESR_Details CheckMode = ESR_Details.Tag;
        [HideInInspector][Port(EPortPinType.Output)] public PGGModCellPort FinalCell;
        [HideInInspector][Tooltip("Checking only directions which back cell is out of grid")] public bool BackEmptyOnly = false;
        [HideInInspector] public int MaxDistance = 64;


        float nearest = float.MaxValue;
        float farthest = float.MinValue;
        string tag = "";
        FieldCell preCell = null;
        FieldCell targetCell = null;
        FieldCell choosedCell = null;


        public override void Execute(PlanGenerationPrint print, PlannerResult newResult)
        {
            if (MG_Cell == null) return;

            nearest = int.MaxValue;
            farthest = int.MinValue;
            choosedCell = null;
            tag = "";

            Tag.TriggerReadPort(true);
            tag = Tag.GetInputValue;

            for (int r = 0; r < 4; r++)
            {
                preCell = null;
                targetCell = null;

                Vector3Int dir = new Vector3Int(0, 0, 0);

                if (r == 0) dir.z = 1;
                else if (r == 1) dir.x = 1;
                else if (r == 2) dir.z = -1;
                else if (r == 3) dir.x = -1;

                if (BackEmptyOnly)
                {
                    FieldCell bCell = MG_Grid.GetCell(MG_Cell.Pos - dir, false);

                    if (!FGenerators.IsNull(bCell))
                    {
                        if (bCell.InTargetGridArea) continue;
                    }
                }

                for (int d = 1; d < MaxDistance; d++)
                {
                    bool breakCheck = CheckCellAt(dir, d);
                    if (breakCheck) break;
                }

                if (FGenerators.NotNull(preCell))
                {
                    float distance = (preCell.Pos - MG_Cell.Pos).magnitude;

                    if (CheckResult == EPurpose.ChooseFarthestDir)
                    {
                        if (CheckResult == EPurpose.ChooseNearestDir_NoZero) if (distance < 2) continue;

                        if (distance > farthest)
                        {
                            choosedCell = preCell;
                            farthest = distance;
                        }
                    }
                    else
                    {
                        if (distance < nearest)
                        {
                            nearest = distance;
                            choosedCell = preCell;
                        }
                    }
                }

            }

            if (FGenerators.NotNull(choosedCell))
            {
                FinalCell.ProvideFullCellData(choosedCell);

                Vector3 diff = choosedCell.Pos - MG_Cell.Pos;
                CellsDistance.Value = Mathf.RoundToInt(diff.magnitude);
                Direction.Value = diff.normalized;
            }

        }

        /// <summary> True if loop should break </summary>
        bool CheckCellAt(Vector3Int offset, int d)
        {
            Vector3Int cellPos = MG_Cell.Pos + offset * d;

            FGenGraph<FieldCell, FGenPoint> grid = MG_Grid;

            var cell = grid.GetCell(cellPos, false);

            if (FGenerators.IsNull(cell)) { CheckCell(cell); return true; }
            if (cell.InTargetGridArea == false) { CheckCell(cell); return true; }

            CheckCell(cell);

            if (StopAt == ESR_Space.Occupied || StopAt == ESR_Space.InGrid)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    return true;
                }
                else
                {
                    if (FGenerators.NotNull(SpawnRuleBase.GetSpawnDataWithSpecifics(cell, tag, CheckMode)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }


        void CheckCell(FieldCell cell)
        {
            preCell = targetCell;
            targetCell = cell;
        }


        #region Editor Code

#if UNITY_EDITOR

        SerializedProperty sp = null;
        SerializedProperty spTag = null;

        public override void Editor_OnNodeBodyGUI(ScriptableObject setup)
        {
            base.Editor_OnNodeBodyGUI(setup);
            baseSerializedObject.Update();

            extraNodeHeight = 0;

            if (StopAt == ESR_Space.InGrid || StopAt == ESR_Space.Occupied)
            {
                if (spTag == null) spTag = baseSerializedObject.FindProperty("Tag");
                EditorGUILayout.PropertyField(spTag);
                var spc = spTag.Copy(); spc.Next(false);
                EditorGUILayout.PropertyField(spc);

                extraNodeHeight += 38;
            }

            if (_EditorFoldout)
            {
                if (sp == null) sp = baseSerializedObject.FindProperty("FinalCell");
                EditorGUILayout.PropertyField(sp);
                var spc = sp.Copy(); spc.Next(false);
                EditorGUILayout.PropertyField(spc);
                FinalCell.AllowDragWire = true;
                extraNodeHeight += 38;
            }
            else
            {
                FinalCell.AllowDragWire = false;
            }

            baseSerializedObject.ApplyModifiedProperties();
        }

#endif

        #endregion

    }
}