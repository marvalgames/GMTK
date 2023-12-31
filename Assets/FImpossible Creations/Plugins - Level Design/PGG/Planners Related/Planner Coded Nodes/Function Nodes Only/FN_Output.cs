﻿using FIMSpace.Graph;
using UnityEngine;
#if UNITY_EDITOR
using FIMSpace.FEditor;
using UnityEditor;
#endif

namespace FIMSpace.Generating.Planning.PlannerNodes.FunctionNode
{

    public class FN_Output : PlannerRuleBase
    {
        [HideInInspector] public string OutputName = "Output";
        public override string GetDisplayName(float maxWidth = 120) { return OutputName; }
        public override string GetNodeTooltipDescription { get { return "Defining output port for other nodes which will use this function node.\nCan be ordered through inspector window if you select this function node file"; } }
        public override EPlannerNodeType NodeType { get { return EPlannerNodeType.Externals; } }
        public override EPlannerNodeVisibility NodeVisibility { get { return EPlannerNodeVisibility.JustFunctions; } }

        public override Vector2 NodeSize { get { return new Vector2(Mathf.Max(170, 40 + OutputName.Length * 12), 84); } }
        public override Color GetNodeColor() { return new Color(.4f, .4f, .4f, .95f); }

        public override bool DrawInspector { get { return true; } }
        public override bool DrawInputConnector { get { return false; } }
        public override bool DrawOutputConnector { get { return false; } }

        public EFunctionPortType OutputType = EFunctionPortType.Number;

        [HideInInspector][Port(EPortPinType.Input, true)] public IntPort IntOut;
        [HideInInspector][Port(EPortPinType.Input, true)] public BoolPort BoolOut;
        [HideInInspector][Port(EPortPinType.Input, true)] public FloatPort FloatOut;
        [HideInInspector][Port(EPortPinType.Input, true)] public PGGVector3Port Vector3Out;
        [HideInInspector][Port(EPortPinType.Input, true)] public PGGStringPort StringOut;
        [HideInInspector][Port(EPortPinType.Input, true)] public PGGCellPort CellOut;
        [HideInInspector][Port(EPortPinType.Input, true)] public PGGPlannerPort FieldOut;

        public void RefreshPortValue()
        {
            var port = GetFunctionOutputPort();

            if (port.IsConnected)
                if (port.BaseConnection != null && port.BaseConnection.PortReference != null)
                {
                    var oPort = port.BaseConnection.PortReference as NodePortBase;
                    if (oPort != null) FN_Input.SetValueToPort(GetFunctionOutputPort(), oPort);
                }
        }

#if UNITY_EDITOR


        public override bool Editor_PreBody()
        {
            Rect r = new Rect(NodeSize.x - 37, 18, 14, 14);

            Color preC = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);

            if (_port2 == null) _port2 = Resources.Load<Texture2D>("ESPR_Input.fw");
            GUI.DrawTexture(r, _port2);

            GUI.color = preC;

            r.size = new Vector2(12, 12);
            r.position = new Vector2(24, r.position.y + 1);

            if (GUI.Button(r, new GUIContent(FGUI_Resources.Tex_Rename), EditorStyles.label))
            {
                string filename = EditorUtility.SaveFilePanelInProject("Type new name (no file will be created)", OutputName, "", "Type new display name for the input (no file will be created)");
                if (!string.IsNullOrEmpty(filename)) OutputName = System.IO.Path.GetFileNameWithoutExtension(filename);
            }

            return false;
        }



        public override void Editor_OnNodeBodyGUI(ScriptableObject setup)
        {
            UnityEditor.EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28);
            OutputType = (EFunctionPortType)UnityEditor.EditorGUILayout.EnumPopup(OutputType, GUILayout.Width(NodeSize.x - 80));
            UnityEditor.EditorGUILayout.EndHorizontal();

            GUILayout.Space(-20);
            NodePortBase port = null;

            IntOut.AllowDragWire = false;
            BoolOut.AllowDragWire = false;
            FloatOut.AllowDragWire = false;
            Vector3Out.AllowDragWire = false;
            StringOut.AllowDragWire = false;
            CellOut.AllowDragWire = false;

            switch (OutputType)
            {
                case EFunctionPortType.Int: port = IntOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("IntOut")); break;
                case EFunctionPortType.Bool: port = BoolOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("BoolOut")); break;
                case EFunctionPortType.Number: port = FloatOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("FloatOut")); break;
                case EFunctionPortType.Vector3: port = Vector3Out; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("Vector3Out")); break;
                case EFunctionPortType.String: port = StringOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("StringOut")); break;
                case EFunctionPortType.Cell: port = CellOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("CellOut")); break;
                case EFunctionPortType.Field: port = FieldOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("FieldOut")); break;
            }

            if (port != null) port.AllowDragWire = true;

        }


        //Texture2D _port = null;
        Texture2D _port2 = null;

        SerializedProperty sp_OutputName = null;
        public override void Editor_OnAdditionalInspectorGUI()
        {
            if (sp_OutputName == null) sp_OutputName = baseSerializedObject.FindProperty("OutputName");
            EditorGUILayout.PropertyField(sp_OutputName);
            GUILayout.Space(4);

            UnityEditor.EditorGUILayout.BeginHorizontal();
            GUILayout.Space(30); OutputType = (EFunctionPortType)UnityEditor.EditorGUILayout.EnumPopup(OutputType, GUILayout.Width(NodeSize.x - 90));
            UnityEditor.EditorGUILayout.EndHorizontal();

            GUILayout.Space(-20);
            NodePortBase port = null;

            IntOut.AllowDragWire = false;
            BoolOut.AllowDragWire = false;
            FloatOut.AllowDragWire = false;
            Vector3Out.AllowDragWire = false;
            StringOut.AllowDragWire = false;
            CellOut.AllowDragWire = false;

            switch (OutputType)
            {
                case EFunctionPortType.Int: port = IntOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("IntOut")); break;
                case EFunctionPortType.Bool: port = BoolOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("BoolOut")); break;
                case EFunctionPortType.Number: port = FloatOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("FloatOut")); break;
                case EFunctionPortType.Vector3: port = Vector3Out; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("Vector3Out")); break;
                case EFunctionPortType.String: port = StringOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("StringOut")); break;
                case EFunctionPortType.Cell: port = CellOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("CellOut")); break;
                case EFunctionPortType.Field: port = FieldOut; EditorGUILayout.PropertyField(baseSerializedObject.FindProperty("FieldOut")); break;
            }

            if (port != null) port.AllowDragWire = true;

            //port._EditorCustomOffset = new Vector2(0, -20);
            if (port != null)
            {
                //port._E_LatestPortRect.position -= new Vector2(0, 22);
                //port._E_LatestCorrectPortRect.position -= new Vector2(0, 22);
                if (port == FieldOut)
                {
                    EditorGUILayout.LabelField("Lastest Value: " + FieldOut.GetNumberedIDArrayString());
                    EditorGUILayout.LabelField("In Val: " + port.GetPortValueSafe);
                    if (FieldOut.GetInputCheckerSafe != null)
                        EditorGUILayout.LabelField("Cells Count: " + FieldOut.GetInputCheckerSafe.AllCells.Count);
                }
                else
                    EditorGUILayout.LabelField("Lastest Value: " + port.GetPortValueSafe);
            }
            //UnityEditor.EditorGUILayout.EndVertical();
        }


#endif


        public NodePortBase GetFunctionOutputPort()
        {
            switch (OutputType)
            {
                case EFunctionPortType.Int: return IntOut;
                case EFunctionPortType.Bool: return BoolOut;
                case EFunctionPortType.Number: return FloatOut;
                case EFunctionPortType.Vector3: return Vector3Out;
                case EFunctionPortType.String: return StringOut;
                case EFunctionPortType.Cell: return CellOut;
                case EFunctionPortType.Field: return FieldOut;
            }

            return null;
        }


    }
}