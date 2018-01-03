﻿using UnityEngine;
using System;
using System.Collections;

namespace TerrainComposer2
{
    public class TC_SelectItem : TC_ItemBehaviour
    {
        [NonSerialized] public new TC_SelectItemGroup parentItem;
        
        public Vector2 range;

        public GameObject oldSpawnObject;
        public Tree tree;
        public SpawnObject spawnObject;
        public Color color = Color.white;
        public int selectIndex;
        public float[] splatCustomValues;
        public bool splatCustom;
        public float splatCustomTotal;

        public int globalListIndex = -1;
        
        [NonSerialized] TC_Settings settings;

        public override void Awake()
        {
            // Debug.Log("Awake SelectItem");
            base.Awake();
            t.hideFlags = HideFlags.NotEditable | HideFlags.HideInInspector;
        }
        
        public override void OnEnable()
        {
            base.OnEnable();
            t.hideFlags = HideFlags.NotEditable | HideFlags.HideInInspector;
        }

        public void CalcSplatCustomTotal()
        {
            splatCustomTotal = 0;
            for (int i = 0; i < splatCustomValues.Length; i++) splatCustomTotal += splatCustomValues[i];
            // Debug.Log(splatCustomTotal);
        }

        public void SetPreviewColor()
        {
            if (outputId == TC.colorOutput) return;

            TC_GlobalSettings globalSettings = TC_Settings.instance.global;

            if (selectIndex == -1) selectIndex = 0;

            if (outputId == TC.splatOutput)
            {
                if (splatCustom)
                {
                    color = Color.black;
                    for (int i = 0; i < splatCustomValues.Length; i++) color += splatCustomValues[i] * globalSettings.GetVisualizeColor(i);
                    color /= splatCustomTotal;
                }
                else color = globalSettings.GetVisualizeColor(selectIndex);
            }
            else if (outputId == TC.treeOutput || outputId == TC.objectOutput) color = globalSettings.GetVisualizeColor(listIndex); else color = globalSettings.GetVisualizeColor(selectIndex);
        }

        public int GetItemTotalFromTerrain()
        {
            TC_Settings localSettings = TC_Settings.instance;

            if (!localSettings.hasMasterTerrain) return 0;

            if (outputId == TC.splatOutput) return localSettings.masterTerrain.terrainData.splatPrototypes.Length;
            if (outputId == TC.grassOutput) return localSettings.masterTerrain.terrainData.detailPrototypes.Length;
            if (outputId == TC.treeOutput) return localSettings.masterTerrain.terrainData.treePrototypes.Length;
            
            return 0;
        }

        public void Refresh()
        {
            // Debug.Log("Refresh");
            // SetPreviewItemTexture();
            // SetPreviewColor();
            // parentItem.RefreshRanges();
            //parentItem.CalcPreview();
            parentItem.GetItems(true, false, false);
        }

        public void SetPreviewItemTexture()
        {
            if (outputId == TC.splatOutput) SetPreviewSplatTexture();
            else if (outputId == TC.colorOutput) SetPreviewColorTexture();
            else if (outputId == TC.treeOutput) SetPreviewTreeTexture();
            else if (outputId == TC.grassOutput) SetPreviewGrassTexture();
            else if (outputId == TC.objectOutput) SetPreviewObjectTexture();
        }

        public void SetPreviewSplatTexture()
        {
            TC_Settings localSettings = TC_Settings.instance;

            if (localSettings.hasMasterTerrain)
            {
                if (selectIndex < localSettings.masterTerrain.terrainData.splatPrototypes.Length && selectIndex >= 0)
                {
                    preview.tex = localSettings.masterTerrain.terrainData.splatPrototypes[selectIndex].texture;
                    if (preview.tex != null) name = Mathw.CutString(preview.tex.name, TC.nodeLabelLength);
                }
                else active = false;
            }
            else preview.tex = null;
        }

        public void SetPreviewTreeTexture()
        {
            #if UNITY_EDITOR
            TC_Settings settings = TC_Settings.instance;

            if (settings.hasMasterTerrain)
            {
                if (selectIndex < settings.masterTerrain.terrainData.treePrototypes.Length && selectIndex >= 0)
                {
                    preview.tex = UnityEditor.AssetPreview.GetAssetPreview(settings.masterTerrain.terrainData.treePrototypes[selectIndex].prefab);
                    name = Mathw.CutString(settings.masterTerrain.terrainData.treePrototypes[selectIndex].prefab.name, TC.nodeLabelLength);
                }
                else active = false;
            }
            else preview.tex = null;
            #endif
        }

        public void SetPreviewObjectTexture()
        {
            #if UNITY_EDITOR
            if (spawnObject == null) { preview.tex = null; return; }
            if (spawnObject.go == null)
            {
                // Debug.Log("preview tex is set to null");
                preview.tex = null;
                return;
            }
            // Debug.Log("Yes");
            preview.tex = UnityEditor.AssetPreview.GetAssetPreview(spawnObject.go);
            name = Mathw.CutString(spawnObject.go.name, TC.nodeLabelLength);
            #endif

            // Debug.Log("SetPreviewObjectTexture");
        }

        public void SetPreviewGrassTexture()
        {
            TC_Settings localSettings = TC_Settings.instance;

            if (localSettings.hasMasterTerrain)
            {
                if (selectIndex < localSettings.masterTerrain.terrainData.detailPrototypes.Length && selectIndex >= 0)
                {
                    DetailPrototype detailPrototype = localSettings.masterTerrain.terrainData.detailPrototypes[selectIndex];
                    if (detailPrototype.usePrototypeMesh)
                    {
                        #if UNITY_EDITOR
                        preview.tex = UnityEditor.AssetPreview.GetAssetPreview(detailPrototype.prototype);
                        #endif
                    }
                    else preview.tex = detailPrototype.prototypeTexture;
                    if (preview.tex != null) name = Mathw.CutString(preview.tex.name, TC.nodeLabelLength);
                }
                else active = false;
            }
            else preview.tex = null;
        }

        public void SetPreviewColorTexture()
        {
            #if UNITY_EDITOR
                if (preview.tex != Texture2D.whiteTexture) preview.tex = Texture2D.whiteTexture;
            #endif
        }
        
        [Serializable]
        public class SpawnObject
        {
            public GameObject go;

            public bool linkToPrefab = false;
            public float randomPosition = 1;
            public Vector2 heightRange = Vector2.zero;
            public bool includeScale = false;
            public float heightOffset = 0;
            public bool includeTerrainHeight = true;
            
            public Vector2 rotRangeX = Vector2.zero;
            public Vector2 rotRangeY = Vector2.zero;
            public Vector2 rotRangeZ = Vector2.zero;
            public bool isSnapRot, isSnapRotX = true, isSnapRotY = true, isSnapRotZ = true;
            public float snapRotX = 45 , snapRotY = 45, snapRotZ = 45;

            public Vector2 scaleRange = Vector2.one;
            public float scaleMulti = 1;
            public float nonUniformScale = 0f;

            public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);

            public Transform lookAtTarget;
            public bool lookAtX;
        }

        [Serializable]
        public class Tree
        {
            public float randomPosition = 1;
            public float heightOffset = 0;

            public Vector2 scaleRange = new Vector2(0.5f, 2f);
            public float nonUniformScale = 0.2f;
            public float scaleMulti = 1;
            public AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);
        }
    }
}