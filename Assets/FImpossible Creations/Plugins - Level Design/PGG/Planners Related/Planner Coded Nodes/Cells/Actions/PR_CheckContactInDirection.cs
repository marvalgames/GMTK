using FIMSpace.Graph;
using UnityEngine;
using FIMSpace.Generating.Checker;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.PlannerNodes.Cells.Actions
{

    public class PR_CheckContactInDirection : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "  Contact In Direction" : "Check Cell Contact In Direction"; }
        public override string GetNodeTooltipDescription { get { return "Checking if there is collision with other field in choosed direction"; } }
        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.CellsManipulation; } }
        public override Color GetNodeColor() { return new Color(0.2f, 0.9f, 0.3f, 0.9f); }
        public override Vector2 NodeSize { get { return new Vector2(254, _EditorFoldout ? 161 : 141); } }
        public override bool IsFoldable { get { return true; } }
        public override bool DrawInputConnector { get { return true; } }
        public override bool DrawOutputConnector { get { return true; } }

        [Port(EPortPinType.Input, "Start Cell")] public PGGCellPort Cell;
        [Port(EPortPinType.Input)] public PGGVector3Port Direction;
        [Port(EPortPinType.Input)] public PGGPlannerPort CheckContactWith;
        [Port(EPortPinType.Output, EPortValueDisplay.HideValue)] public PGGCellPort Contact;

        [Tooltip("Stopping checking if there is self cell on direction step check.")]
        [HideInInspector] public bool StopOnSelfCollision = false;

        private FieldPlanner latestCaller = null;

        public override void Prepare(PlanGenerationPrint print)
        {
            latestCaller = null;
            base.Prepare(print);
        }

        public override void OnStartReadingNode()
        {
            base.OnStartReadingNode();

            if (CurrentExecutingPlanner == null) return;

            if (InputConnections.Count == 0)
            {
                if ( Contact.IsConnected)
                {
                    Execute(null, CurrentExecutingPlanner.LatestResult);
                }
            }
        }

        public override void Execute(PlanGenerationPrint print, PlannerResult newResult)
        {
            Contact.Clear();
            _breakIt = false;

            // Call for contact mask only once for the planner
            if (latestCaller != newResult.ParentFieldPlanner)
            {
                CheckContactWith.TriggerReadPort(true);
                latestCaller = newResult.ParentFieldPlanner;
            }

            System.Collections.Generic.List<FieldPlanner> contactMask = GetPlannersFromPort(CheckContactWith, false, false);


            if (contactMask == null) { return; }
            if (contactMask.Count == 0) { return; }

            Cell.TriggerReadPort(true);
            FieldCell myCell = Cell.GetInputCellValue;
            if (FGenerators.CheckIfIsNull(myCell)) { return; }

            Direction.TriggerReadPort(true);
            Vector3Int dir = Direction.GetInputValue.V3toV3Int();

            if (dir == Vector3Int.zero) { return; }

            float nrst = float.MaxValue;
            Checker.CheckerField3D myChecker = Cell.GetInputCheckerValue;

            if (myChecker == null) { return; }
            _selfChecker = myChecker;

            Checker.CheckerField3D.DebugHelper = false;

            FieldPlanner cellPlanner = Cell.GetInputPlannerIfPossible;

            if (cellPlanner)
            {
                if (contactMask.Count == 1 && contactMask[0] == cellPlanner && CheckContactWith.PortState() == EPortPinState.Connected)
                {
                    var selfContact = GetPlannerFromPort(CheckContactWith);
                    if (selfContact != cellPlanner)
                        contactMask.Remove(cellPlanner);
                }
                else
                    contactMask.Remove(cellPlanner);
            }


            for (int i = 0; i < contactMask.Count; i++)
            {
                Checker.CheckerField3D othChecker = contactMask[i].LatestResult.Checker;

                if (myChecker.CheckCollisionInDirection(myCell, dir, othChecker, 32, true, CallbackCheck))
                {
                    if (_breakIt) break;

                    FieldCell oCell = myChecker._CheckCollisionInDirection_OtherCell;

                    float dist = (othChecker.GetWorldPos(oCell) - myChecker.GetWorldPos(myCell)).sqrMagnitude;

                    if (dist < nrst)
                    {
                        nrst = dist;
                        Contact.ProvideFullCellData(myChecker._CheckCollisionInDirection_OtherCell, othChecker, contactMask[i].LatestResult);
                    }
                }
            }

            #region Debugging Gizmos
#if UNITY_EDITOR
            if (Debugging)
            {
                DebuggingInfo = "Outside direction iteration";

                Vector3 wPos = myChecker.GetWorldPos(myCell);
                Vector3 owPos = Vector3.zero;
                Vector3 scl = myChecker.RootScale;

                bool detected = false;
                if (FGenerators.Exists(Contact.Cell))
                    if (FGenerators.Exists(Contact.Checker))
                    {
                        detected = true;
                        owPos = Contact.Checker.GetWorldPos(Contact.Cell);
                    }

                DebuggingGizmoEvent = () =>
                {
                    Gizmos.color = detected ? Color.green : Color.yellow;
                    Gizmos.DrawCube(wPos, scl * 0.5f);

                    if (detected)
                    {
                        Gizmos.DrawCube(wPos, scl * 0.5f);
                        Gizmos.DrawCube(owPos, scl * 0.5f);
                        Gizmos.DrawLine(wPos, owPos);
                    }
                };
            }
#endif
            #endregion

        }

        bool _breakIt = false;
        CheckerField3D _selfChecker = null;
        void CallbackCheck(Vector3 pos)
        {
            if (StopOnSelfCollision == false) return;
            if (_selfChecker == null) return;

            if (_selfChecker.ContainsWorld(pos))
            {
                _breakIt = true;
            }
        }

#if UNITY_EDITOR

        public override void Editor_OnAdditionalInspectorGUI()
        {
            EditorGUILayout.LabelField("Debugging:", EditorStyles.helpBox);
            GUILayout.Label("Cell: " + Cell.GetPortValueSafe);
        }

        SerializedProperty sp_Extra = null;
        public override void Editor_OnNodeBodyGUI(ScriptableObject setup)
        {
            base.Editor_OnNodeBodyGUI(setup);

            baseSerializedObject.Update();

            if ( _EditorFoldout)
            {
                if (sp_Extra == null) sp_Extra = baseSerializedObject.FindProperty("StopOnSelfCollision");
                EditorGUILayout.PropertyField(sp_Extra);
            }

            baseSerializedObject.ApplyModifiedProperties();
        }

#endif

    }
}