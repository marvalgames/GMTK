#if UNITY_EDITOR
using FIMSpace.FEditor;
using UnityEditor;
#endif
using UnityEngine;
using System;
using System.Collections.Generic;

namespace FIMSpace.Generating.Rules.QuickSolutions
{
    public class SR_StairsHelper : SpawnRuleBase, ISpawnProcedureType
    {
        public override string TitleName() { return "Stairs Placer Helper"; }
        public override string Tooltip() { return "Quick solution for finding conditions for the stairs placement"; }
        public EProcedureType Type { get { return EProcedureType.Coded; } }

        [PGG_SingleLineSwitch("CheckMode", 50, "Select if you want to use Tags, SpawnStigma or CellData", 110)]
        [HideInInspector] public string RemoveTagged = "";
        [HideInInspector] public ESR_Details CheckMode = ESR_Details.Tag;

        [Tooltip("Distance range to detect spawns to remove")]
        [HideInInspector] public float RemoveDetectionTolerance = 0f;

        [PGG_SingleLineSwitch("StopOnCheckMode", 50, "Select if you want to use Tags, SpawnStigma or CellData", 110)]
        [HideInInspector] public string StopOnTagged = "";
        [HideInInspector] public ESR_Details StopOnCheckMode = ESR_Details.Tag;

        //[Space(5)]
        //[Tooltip("Displaying measuring positions debug rays for about a second, after calling generating.\nGreen is 'WallPlacementGuide' position and yellow are spawns detected positions.\nGenerator need to be placed in zero position and zero rotation to align. To test it best will be Grid Painter component.")]
        //public bool DebugDrawRays = false;

        [HideInInspector] public CheckCellsSelectorSetup checkSetup = new CheckCellsSelectorSetup(false, false);
        [HideInInspector] public CheckCellsSelectorSetup removeInCells = new CheckCellsSelectorSetup(false, false);
        [HideInInspector] public CheckCellsSelectorSetup breakOnCells = new CheckCellsSelectorSetup(false, false);

        [HideInInspector] public bool wasGenerated = false;
        [Tooltip("If 'Hard Break' should completely prevent spawning any tile at any rotation.")]
        [HideInInspector] public bool doFullBreak = false;


        #region Editor Code

#if UNITY_EDITOR

        SerializedProperty sp = null;
        SerializedProperty spdoFullBreak = null;
        GUIContent _guic = null;

        public override void NodeBody(SerializedObject so)
        {
            EditorGUILayout.HelpBox("Checking 4 directions (360) with selected offsets for target stairs step shape.", MessageType.None);

            if (!wasGenerated)
            {
                if (!checkSetup.ToCheck.Contains(Vector3Int.zero)) checkSetup.ToCheck.Add(Vector3Int.zero);
                checkSetup.ToCheck.Add(new Vector3Int(0, 0, -1));
                checkSetup.ToCheck.Add(new Vector3Int(0, 1, 0));
                checkSetup.ToCheck.Add(new Vector3Int(0, 1, 1));

                if (!removeInCells.ToCheck.Contains(Vector3Int.zero)) removeInCells.ToCheck.Add(Vector3Int.zero);
                removeInCells.ToCheck.Add(new Vector3Int(0, 1, 0));

                breakOnCells.ToCheck.Clear();
                wasGenerated = true;
            }

            if (sp == null) sp = so.FindProperty("RemoveTagged");

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();

            int size = 148;
            EditorGUILayout.BeginVertical(GUILayout.Width(size), GUILayout.Height(size));
            GUILayout.Label("", GUILayout.Width(size));
            var lastRect = GUILayoutUtility.GetLastRect();
            lastRect.size = new Vector2(size, size);
            GUI.Box(lastRect, GUIContent.none, EditorStyles.helpBox);
            PreviewWindow(lastRect);

            //if (GUI.Button(lastRect, GUIContent.none, EditorStyles.label)) { CheckCellsSelectorWindow.Init(checkSetup.ToCheck, OwnerSpawner, checkSetup); }

            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            EditorGUILayout.BeginVertical();

            checkSetup.UseRotor = false;
            checkSetup.UseCondition = false;

            if (_guic == null || _guic.image == null) { _guic = new GUIContent("  Display Cells Step Selector", PGGUtils.Tex_Selector); }
            _guic.text = "  Display Cells Step Selector";
            if (GUILayout.Button(_guic)) { CheckCellsSelectorWindow.Init(checkSetup.ToCheck, OwnerSpawner, checkSetup); }
            GUILayout.Space(5);

            EditorGUILayout.PropertyField(sp);
            var spc = sp.Copy();
            EditorGUIUtility.labelWidth = 180;

            spc.Next(false); spc.Next(false); EditorGUILayout.PropertyField(spc);


            //spc.Next(false); spc.Next(false); EditorGUILayout.PropertyField(spc);
            //spc.Next(false); EditorGUILayout.PropertyField(spc);

            EditorGUILayout.BeginHorizontal();

            removeInCells.UseRotor = false;
            removeInCells.UseCondition = false;
            removeInCells.EditorDisplayColor = new Color(0.8f, 0.5f, 0.4f, 0.75f);
            _guic.text = "  Remove In Cells (" + removeInCells.ToCheck.Count + ")";
            GUILayout.Space(5);
            GUI.backgroundColor = new Color(1f, 0.85f, 0.75f, 1f);
            if (GUILayout.Button(_guic)) { CheckCellsSelectorWindow.Init(removeInCells.ToCheck, OwnerSpawner, removeInCells); }


            if (RemoveDetectionTolerance <= 0f)
            {
                RemoveDetectionTolerance = 0f;
                EditorGUILayout.HelpBox("Distance Check OFF", MessageType.None);
            }

            EditorGUILayout.EndHorizontal();


            GUILayout.Space(8);
            breakOnCells.UseRotor = false;
            breakOnCells.UseCondition = false;
            breakOnCells.EditorDisplayColor = new Color(1f, 0.1f, 0.1f, 1f);
            _guic.text = "  Hard Stop If Cells In (" + breakOnCells.ToCheck.Count + ")";
            if (GUILayout.Button(_guic)) { CheckCellsSelectorWindow.Init(breakOnCells.ToCheck, OwnerSpawner, breakOnCells); }

            spc.Next(false); EditorGUILayout.PropertyField(spc);
            //if (breakOnCells.ToCheck.Count > 0)
            //{
            //    if (spdoFullBreak == null) spdoFullBreak = so.FindProperty("doFullBreak");
            //    EditorGUILayout.PropertyField(spdoFullBreak);
            //}

            EditorGUILayout.EndVertical();
            EditorGUIUtility.labelWidth = 0;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            base.NodeBody(so);
        }


        #region 3D Preview Window


        SimpleGUI3DView previewIn3D = null;
        protected void PreviewWindow(Rect r)
        {
            if (previewIn3D == null) previewIn3D = (SimpleGUI3DView)Editor.CreateEditor(this, typeof(SimpleGUI3DView));
            if (previewIn3D == null) return;

            previewIn3D.HandlesAction = () =>
            {
                Handles.color = new Color(0.4f, 0.4f, 1f, 0.8f);

                for (int i = 0; i < checkSetup.ToCheck.Count; i++)
                    Handles.CubeHandleCap(0, checkSetup.ToCheck[i], Quaternion.identity, 0.75f, EventType.Repaint);



                Handles.color = new Color(0.9f, 0.6f, 0.3f, 0.5f);

                for (int i = 0; i < removeInCells.ToCheck.Count; i++)
                    Handles.CubeHandleCap(0, removeInCells.ToCheck[i], Quaternion.identity, 0.5f, EventType.Repaint);


                Handles.color = new Color(1f, 0.1f, 0.1f, 0.9f);

                if (string.IsNullOrEmpty(StopOnTagged))
                {
                    for (int i = 0; i < breakOnCells.ToCheck.Count; i++) Handles.CubeHandleCap(0, breakOnCells.ToCheck[i], Quaternion.identity, 0.9f, EventType.Repaint);
                }
                else
                {
                    Vector3 size = Vector3.one * 0.9f;
                    for (int i = 0; i < breakOnCells.ToCheck.Count; i++) Handles.DrawWireCube(breakOnCells.ToCheck[i], size);
                }


                Handles.color = new Color(0.8f, 0.8f, 1f, 0.5f);
                Handles.DrawWireCube(Vector3.zero, new Vector3(1f, 0.1f, 1f));
            };

            previewIn3D.UpdateInput = r.Contains(Event.current.mousePosition);
            previewIn3D.OnInteractivePreviewGUI(r, EditorStyles.textArea);
        }


        #endregion


#endif

        #endregion


        List<FieldCell> toRemove = null;
        int foundAtRotation = -1;
        bool fullBreak = false;
        public override void CheckRuleOn(FieldModification mod, ref SpawnData spawn, FieldSetup preset, FieldCell cell, FGenGraph<FieldCell, FGenPoint> grid, Vector3? restrictDirection = null)
        {
            if (toRemove == null) toRemove = new List<FieldCell>();

            base.CheckRuleOn(mod, ref spawn, preset, cell, grid, restrictDirection);

            foundAtRotation = -1;
            CellAllow = false;
            fullBreak = false;

            for (int r = 0; r <= 270; r += 90)
            {
                Quaternion rot = Quaternion.Euler(0f, r, 0f);

                if (GetCellsAt(rot, cell, grid))
                {
                    if (foundAtRotation == -1) foundAtRotation = r;
                    if (doFullBreak == false) break;
                }

                if (fullBreak)
                {
                    CellAllow = false;
                    return;
                }
            }

            if (fullBreak)
            {
                CellAllow = false;
                return;
            }
            if (foundAtRotation == -1) return;

            CellAllow = true;
        }



        public override void OnConditionsMetAction(FieldModification mod, ref SpawnData spawn, FieldSetup preset, FieldCell cell, FGenGraph<FieldCell, FGenPoint> grid)
        {
            if (toRemove == null) toRemove = new List<FieldCell>();

            Quaternion rot = Quaternion.Euler(0f, foundAtRotation, 0f);
            spawn.RotationOffset = new Vector3(0, foundAtRotation, 0);

            Vector3 centeredMainSpawnPosition = Vector3.zero;
            if (RemoveDetectionTolerance > 0f)
            {
                centeredMainSpawnPosition = GetCenteredSpawnPosition(spawn, ESR_Origin.RendererCenter);
            }

            for (int r = 0; r < removeInCells.ToCheck.Count; r++)
            {
                Vector3 offset = (rot * removeInCells.ToCheck[r]).V3toV3Int();
                Vector3Int targetCellPos = cell.Pos + offset.V3toV3Int();
                FieldCell rCell = grid.GetCell(targetCellPos, false);
                if (!FieldCell.IsAvailableForExecution(rCell)) continue;

                offset = Vector3.Scale(offset, preset.GetCellUnitSize());
                ProceedRemovingInCell(rCell, offset, centeredMainSpawnPosition);
            }
        }


        void ProceedRemovingInCell(FieldCell cell, Vector3 dirOffset, Vector3 centeredMainSpawnPosition)
        {
            if (RemoveDetectionTolerance <= 0f)
            {
                if (string.IsNullOrEmpty(RemoveTagged))
                {
                    cell.RemoveAllSpawnsFromCell();
                    return;
                }
                else
                {
                    var spawns = cell.CollectSpawns();
                    for (int s = spawns.Count - 1; s >= 0; s--)
                    {
                        if (SpawnHaveSpecifics(spawns[s], RemoveTagged, CheckMode)) spawns.RemoveAt(s);
                    }
                }
            }
            else // Distance Check
            {
                var spawns = cell.CollectSpawns();

                for (int s = spawns.Count - 1; s >= 0; s--)
                {
                    if (!string.IsNullOrEmpty(RemoveTagged)) if (!SpawnHaveSpecifics(spawns[s], RemoveTagged, CheckMode)) continue; // No wanted tag

                    Vector3 spawnPosition = GetCenteredSpawnPosition(spawns[s], ESR_Origin.RendererCenter);
                    spawnPosition += dirOffset;

                    float distance = Vector3.Distance(centeredMainSpawnPosition, spawnPosition);
                    if (distance > RemoveDetectionTolerance) continue;

                    spawns.RemoveAt(s);
                }
            }
        }

        bool GetCellsAt(Quaternion rotation, FieldCell originCell, FGenGraph<FieldCell, FGenPoint> grid)
        {
            for (int i = 0; i < checkSetup.ToCheck.Count; i++)
            {
                Vector3Int targetCellPos = originCell.Pos + (rotation * checkSetup.ToCheck[i]).V3toV3Int();
                FieldCell cell = grid.GetCell(targetCellPos, false);
                if (!FieldCell.IsAvailableForExecution(cell)) return false;

                #region Break On Cells condition check

                for (int b = 0; b < breakOnCells.ToCheck.Count; b++)
                {
                    Vector3Int pos = originCell.Pos + (rotation * breakOnCells.ToCheck[b]).V3toV3Int();
                    FieldCell bCell = grid.GetCell(pos, false);

                    if (string.IsNullOrEmpty(StopOnTagged))
                    {
                        if (FieldCell.IsAvailableForExecution(bCell))
                        {
                            fullBreak = doFullBreak;
                            return false;
                        }
                    }
                    else
                    {
                        if (FieldCell.IsAvailableForExecution(bCell))
                        {
                            var breakSpawns = bCell.CollectSpawns();
                            for (int bs = 0; bs < breakSpawns.Count; bs++)
                            {
                                if (SpawnHaveSpecifics(breakSpawns[bs], StopOnTagged, StopOnCheckMode))
                                {
                                    fullBreak = doFullBreak;
                                    return false;
                                }
                            }
                        }
                    }

                }

                #endregion

            }

            return true;
        }


    }
}