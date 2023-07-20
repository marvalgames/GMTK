using FIMSpace.Graph;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.PlannerNodes.Cells
{

    public class PR_GetCellInstructionParams : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "Instruction Parameters" : "Get Cell Instruction Parameters"; }
        public override string GetNodeTooltipDescription { get { return "Accessing some parameters of provided cell instruction reference"; } }
        public override Color GetNodeColor() { return new Color(0.64f, 0.9f, 0.0f, 0.9f); }
        public override Vector2 NodeSize { get { return new Vector2(200, 120); } }
        public override bool DrawInputConnector { get { return false; } }
        public override bool DrawOutputConnector { get { return false; } }
        public override bool IsFoldable { get { return false; } }

        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.ReadData; } }


        [Port(EPortPinType.Input)] public PGGUniversalPort CellInstruction;
        [Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.NotEditable)] public IntPort InstructionID;
        [Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.NotEditable)] public PGGVector3Port Direction;

        public override void OnStartReadingNode()
        {
            CellInstruction.TriggerReadPort(true);

            object cellVal = CellInstruction.GetPortValueSafe;
            if (cellVal is SpawnInstructionGuide)
            {
                SpawnInstructionGuide guide = cellVal as SpawnInstructionGuide;
                InstructionID.Value = guide.Id;

                if (guide.UseDirection)
                    Direction.Value = guide.rot * Vector3.forward;
                else
                    Direction.Value = Vector3.zero;
            }

        }

    }
}