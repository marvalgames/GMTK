using FIMSpace.Graph;
using System.Collections.Generic;
using UnityEngine;

namespace FIMSpace.Generating.Planning.PlannerNodes.BuildSetup
{

    public class PR_ChooseFields : PlannerRuleBase
    {
        public override string GetDisplayName(float maxWidth = 120) { return wasCreated ? "Choose Fields" : "Choose Fields with Condition"; }
        public override string GetNodeTooltipDescription { get { return "Checking provided group of fields and re-grouping them using some condition"; } }
        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.ReadData; } }
        public override Color GetNodeColor() { return new Color(1.0f, 0.75f, 0.25f, 0.9f); }
        public override Vector2 NodeSize { get { return new Vector2(246, 142); } }

        public override bool DrawInputConnector { get { return false; } }
        public override bool DrawOutputConnector { get { return false; } }

        [Port(EPortPinType.Input, EPortNameDisplay.Default, EPortValueDisplay.Default, "Fields to Check")] public PGGPlannerPort FieldsToIterate;
        [Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.HideValue, "Checked Field")] public PGGPlannerPort IterationField;
        [Port(EPortPinType.Input, EPortNameDisplay.Default, EPortValueDisplay.HideValue, "Add Checked Field?")] public BoolPort AddCondition;

        [Port(EPortPinType.Output, EPortNameDisplay.Default, EPortValueDisplay.HideValue, "Choosen Fields")] public PGGPlannerPort ChoosenFields;

        public override void OnCreated()
        {
            AddCondition.Value = false;
            base.OnCreated();
        }

        public override void DONT_USE_IT_YET_OnReadPort(IFGraphPort port)
        {
            if (port != ChoosenFields) return;

            AddCondition.Value = false;
            ChoosenFields.Clear();

            if (FieldsToIterate.IsNotConnected)
            {
                var planner = GetPlannerFromPort(FieldsToIterate);
                if (planner != null) IterateList(new List<FieldPlanner>() { planner });
                return;
            }

            FieldsToIterate.TriggerReadPort(true);
            List<FieldPlanner> planners = GetPlannersFromPort(FieldsToIterate, false, false);

            IterationField.Switch_ReturnOnlyCheckers = false;

            IterateList(planners);
        }

        void IterateList(List<FieldPlanner> planners)
        {
            if (planners == null) return;
            if (planners.Count == 0) return;

            List<FieldPlanner> choosen = new List<FieldPlanner>();

            for (int c = 0; c < planners.Count; c++)
            {
                if (planners[c].Available == false) continue;

                IterationField.SetIDsOfPlanner(planners[c]);
                //IterationField.SetIDsOfPlanner(planners[c]);

                AddCondition.TriggerReadPort(true);
                if (AddCondition.GetInputValue) choosen.Add(planners[c]);
            }

            ChoosenFields.AssignPlannersList(choosen);
        }


    }
}