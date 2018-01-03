﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading;


namespace TerrainComposer2
{
    [ExecuteInEditMode]
    public class TC_Generate : MonoBehaviour
    {
        public static TC_Generate instance;
        public float globalScale = 1;
        public TC_Area2D area2D;
        public bool assignTerrainHeightmap;
        public bool hideHierarchy;
        public bool generate;
        public bool generateSplat;
        public bool generateSplatSingle;
        public bool generateTree;
        public bool generateObject;
        public bool generateGrass;
        public bool generateColor;
        public bool resetTrees;
        public bool generateSingle;
        public int threadActive = 0;
        public bool isMesh;
        public bool resetObjects;

        public bool autoGenerate;
        public bool cmdGenerate;
        public bool generateNextFrame;
        public int generateDone;
        public int generateDoneOld;
        public int treesCount, objectsCount;

        public bool isGeneratingHeight;

        public int jobs = 0;
        float[] heightsReadback;
        float[,] heights;
        int[,] grass;
        List<TreeInstance> trees;

        public List<GenerateStackEntry> stackEntry = new List<GenerateStackEntry>();

        public Transform objectParent;

        [System.NonSerialized] TC_Terrain firstTreeTerrain;
        [System.NonSerialized] TC_Terrain firstObjectTerrain;
        
        // static public EditorCoroutine co;

        void OnEnable()
        {
            instance = this;
            isGeneratingHeight = false;

            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update += MyUpdate;
            #endif
        }

        void OnDisable()
        {
            #if UNITY_EDITOR    
            UnityEditor.EditorApplication.update -= MyUpdate;
            #endif
        }

        void OnDestroy()
        {
            instance = null;
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= MyUpdate;
            #endif
        }

        int frame = 0;
        bool updateCamCapture;

        #if !UNITY_EDITOR
        public void Update()
        {
            MyUpdate();
        }
        #endif
        
        public void MyUpdate()
        {

            TC_Settings settings = TC_Settings.instance;

            if (settings != null)
            {
                if (settings.version == 0)
                {
                    settings.version = TC.GetVersionNumber();
                    TC.refreshOutputReferences = 7;
                    TC.AddMessage("TerrainComposer2 is updated to " + TC.GetVersionLabel());
                }
            }

            area2D = TC_Area2D.current;

            if (area2D == null) return;
            if (area2D.terrainLayer == null) return;
            if (area2D.terrainLayer.layerGroups[0] == null) TC.refreshOutputReferences = TC.allOutput;

            RefreshOutputReferences(TC.refreshOutputReferences, TC.refreshPreviewImages);

            if (cmdGenerate)
            {
                cmdGenerate = false;

                if (autoGenerate)
                {
                    TC_Reporter.Log("Generate from auto", 2);
                    Generate(false);
                }
                else TC.autoGenerateCallTimeStart = Time.realtimeSinceStartup;
            }

            generate = false;

            // if (frame >= 0)
            //{
                RunGenerateStack();
                // frame = 0;
            // }
            ++frame;
        }
        
        public void RefreshOutputReferences(int outputId, bool refreshPreviewImages)
        {
            if (outputId >= 0)
            {
                TC.refreshPreviewImages = refreshPreviewImages;
                // Debug.Log("GetItems " + TC.refreshOutputReferences);
                
                if (outputId >= 6) area2D.terrainLayer.GetItems(false, true, outputId == 7); else area2D.terrainLayer.GetItem(outputId, true, false);
                TC.refreshOutputReferences = -1;
                TC.refreshPreviewImages = false;
                TC.repaintNodeWindow = true;
            }
        }

        public void RunGenerateStack()
        {
            // if (stack.Count > 0) Debug.Log(stack.Count);

            if (stackEntry.Count > 0)
            {
                if (stackEntry[0].stack.Count == 0) stackEntry.RemoveAt(0);
            }

            if (stackEntry.Count > 0 && !generate)
            {
                // Debug.Log(stack.Count);
                List<GenerateStack> stack = stackEntry[0].stack;
                
                int outputId = stack[0].outputId;
                TCUnityTerrain tcTerrain = stack[0].tcTerrain;
                assignTerrainHeightmap = stack[0].assignTerrainHeightmap;
                stack.RemoveAt(0);
                // Debug.Log(stack[0].tcTerrain.terrain.name);
                GenerateTerrain(tcTerrain, outputId);
                Compute(outputId);                
            }
        }

        public void Compute(int outputId)
        {
            // Debug.Log(frame);
            if (outputId == TC.heightOutput) ComputeHeight();
            else
            {
                if (area2D.currentTCTerrain.texHeight == null)
                {
                    // TC_Compute.instance.RunTerrainTexFromTerrainData(area2D.currentTerrain.terrainData, ref area2D.currentTCTerrain.rtHeight);
                    TC.AddMessage("The 'Height' output is not generated. Currently this is needed, in the next version this will work on existing terrain heightmaps.", 0, 4);
                }
            }

            if (outputId == TC.splatOutput) ComputeSplat();
            else if (outputId == TC.colorOutput) ComputeColor();
            else if (outputId == TC.treeOutput) ComputeTree();
            else if (outputId == TC.grassOutput) ComputeGrass();
            else if (outputId == TC.objectOutput) ComputeObject();
        }

        public bool CheckForTerrain(bool selectTerrainArea = true)
        {
            if (area2D.terrainAreas == null) area2D.terrainAreas = new TC_TerrainArea[1];
            else if (area2D.terrainAreas.Length == 0) area2D.terrainAreas = new TC_TerrainArea[1];

            if (area2D.terrainAreas[0] == null)
            {
                GameObject go = GameObject.Find("Terrain Area");
                if (go != null)
                {
                    TC_TerrainArea terrainArea = go.GetComponent<TC_TerrainArea>();
                    if (terrainArea != null) area2D.terrainAreas[0] = terrainArea;
                    else
                    {
                        TC.AddMessage("The Terrain Area GameObject is missing the 'TC_TerrainArea' script.");
                        #if UNITY_EDITOR
                        if (UnityEditor.EditorUtility.DisplayDialog("The Terrain Area GameObject is missing the 'TC_TerrainArea' script.", "Do you want to TC2 to add the 'TC_TerrainArea' script now?", "Yes", "Cancel"))
                        {
                            area2D.terrainAreas[0] = go.AddComponent<TC_TerrainArea>();
                        }
                        else return false;
                        #else
                            return false;
                        #endif
                    }
                }
                else { TC.AddMessage("No Terrain Area is created."); return false; }
            }
            bool hasTerrain = true;
            if (area2D.terrainAreas[0].terrains.Count == 0)
            {
                area2D.terrainAreas[0].terrains.Add(new TCUnityTerrain());
                hasTerrain = false;
            }

            for (int i = 0; i < area2D.terrainAreas[0].terrains.Count; i++)
            {
                if (!area2D.terrainAreas[0].terrains[i].CheckValidUnityTerrain())
                {
                    if (selectTerrainArea) TC.AddMessage("Terrain missing on X" + area2D.terrainAreas[0].terrains[i].tileX + "_Y"+area2D.terrainAreas[0].terrains[i].tileZ + "\n\nTC2 has automatically selected the Terrain Area GameObject.");
                    hasTerrain = false;
                }
            }

            if (!hasTerrain)
            {
                TC.AddMessage("Please create a terrain first.");
                #if UNITY_EDITOR
                    if (selectTerrainArea) UnityEditor.Selection.activeGameObject = area2D.terrainAreas[0].gameObject;
                #endif
                return false;
            }
            return hasTerrain;
        }

        public void Generate(bool instantGenerate, int outputId = TC.allOutput)
        {
            // Debug.Log("Generate");
            if (!CheckForTerrain()) return;
            area2D = TC_Area2D.current;
            
            TC_Settings settings = TC_Settings.instance;
            if (settings == null)
            {
                TC.AddMessage("Settings GameObject not found."); return;
            }
            
            isMesh = false;
            bool firstTerrain = true;

            for (int i = 0; i < area2D.terrainAreas[0].terrains.Count; i++)
            {
                TCUnityTerrain tcTerrain = area2D.terrainAreas[0].terrains[i];

                if (tcTerrain.active)
                {
                    if (firstTerrain)
                    {
                        if (area2D.terrainLayer.layerGroups[TC.treeOutput].active && (outputId == TC.allOutput || outputId == TC.treeOutput)) firstTreeTerrain = tcTerrain;
                        if (area2D.terrainLayer.layerGroups[TC.objectOutput].active && (outputId == TC.allOutput || outputId == TC.objectOutput)) firstObjectTerrain = tcTerrain;
                        firstTerrain = false;
                    }

                    TC_Compute.instance.camCapture.collisionMask = 0;

                    if (outputId == TC.allOutput) Generate(tcTerrain, instantGenerate);
                    else
                    {
                        if (outputId == TC.heightOutput) GenerateHeight(tcTerrain, instantGenerate, false); else  GenerateOutput(tcTerrain, outputId, instantGenerate);
                    }
                }
            }
        }

        public void GenerateMesh()
        {
            isMesh = true;
        }
        
        public void Generate(TCUnityTerrain tcTerrain, bool instantGenerate)
        {
            // Debug.Log(instantGenerate);
            // if (disableTerrain) tcTerrain.terrain.enabled = false;
            
            if (area2D.terrainLayer.layerGroups[TC.heightOutput].active || area2D.terrainLayer.layerGroups[TC.objectOutput].active) GenerateHeight(tcTerrain, instantGenerate, false);
            
            for (int i = 1; i <= 4; i++)
            {
                if (area2D.terrainLayer.layerGroups[i].active) GenerateOutput(tcTerrain, i, instantGenerate);
            }
            // TC.repaintNodeWindow = true;
        }

        public void GenerateOutput(TCUnityTerrain tcTerrain, int outputId, bool instantGenerate)
        {
            if (area2D.terrainLayer.layerGroups[outputId] != null)
            {
                // Debug.Log("Generate "+generate+ " +"+instantGenerate);

                if (generate && !instantGenerate)
                {
                    bool addToStack = true;
                    
                    if (stackEntry.Count == 0) stackEntry.Add(new GenerateStackEntry(frame));
                    else
                    {
                        if (stackEntry[0].frame != frame && stackEntry.Count == 1)
                        {
                            stackEntry.Add(new GenerateStackEntry(frame));
                        }
                    }

                    if (stackEntry.Count == 2)
                    {
                        List<GenerateStack> stack = stackEntry[1].stack;

                        for (int i = 0; i < stack.Count; i++)
                        {
                            if (stack[i].tcTerrain == tcTerrain && stack[i].outputId == outputId && stack[i].assignTerrainHeightmap == assignTerrainHeightmap) { addToStack = false; break; }
                        }
                    }
                    if (addToStack)
                    {
                        List<GenerateStack> stack = stackEntry[stackEntry.Count - 1].stack;
                        stack.Add(new GenerateStack(outputId, tcTerrain, assignTerrainHeightmap));
                    }
                }
                else
                {
                    // Debug.Log("Generate Output");
                    ++tcTerrain.tasks;
                    GenerateTerrain(tcTerrain, outputId);
                    Compute(outputId);
                }
            }
        }

        public void GenerateHeight(TCUnityTerrain tcTerrain, bool instantGenerate, bool disableTerrain)
        {
            // Debug.Log(instantGenerate);
            // if (disableTerrain) tcTerrain.terrain.enabled = false;
            TC_Compute.instance.camCapture.collisionMask = 0;

            assignTerrainHeightmap = true;

            if (area2D.terrainLayer.layerGroups[TC.heightOutput].active && area2D.terrainLayer.layerGroups[TC.objectOutput].active)
            {
                if (area2D.terrainLayer.layerGroups[TC.heightOutput].ContainsCollisionNode()) assignTerrainHeightmap = false;
            }

            if (area2D.terrainLayer.layerGroups[TC.heightOutput].active) GenerateOutput(tcTerrain, TC.heightOutput, instantGenerate);
            if (area2D.terrainLayer.layerGroups[TC.objectOutput].active) GenerateOutput(tcTerrain, TC.objectOutput, instantGenerate);

            if (!assignTerrainHeightmap)
            {
                // Debug.Log("Second pass");
                assignTerrainHeightmap = true;
                GenerateOutput(tcTerrain, TC.heightOutput, instantGenerate);
            }
            // TC.repaintNodeWindow = true;
        }

        public bool GenerateStart()
        {
            TC_Area2D.current = area2D;
            // area2D.currentTerrainArea = area2D.terrainAreas[0];
            // Debug.Log("Generate Start");

            if (area2D.terrainAreas[0] == null) return false;
            
            if (area2D.previewArea != null)
            {
                if (area2D.previewArea.manual) area2D.SetManualTotalArea();
                else if (!area2D.CalcTotalArea()) return false;
            }
            else if (!area2D.CalcTotalArea()) return false;

            area2D.terrainsDone = 0;
            area2D.terrainsToDo = 0;
            generate = true;
            for (int i = 0; i < area2D.terrainAreas.Length; i++) area2D.terrainsToDo += area2D.terrainAreas[i].terrains.Count;

            return true;
        }

        public void GenerateStop()
        {
            ++generateDone;
        }

        public void ComputeTerrainAreas()
        {
            if (!GenerateStart()) return;

            for (int j = 0; j < area2D.terrainAreas.Length; j++)
            {
                area2D.currentTerrainArea = area2D.terrainAreas[j];
                for (int i = 0; i < area2D.currentTerrainArea.terrains.Count; i++)
                {
                    // if (ComputeTerrain(, area2D.currentTerrainArea.terrains[i], false)) ++area2D.terrainsDone;
                }
            }

            // isGeneratingHeight = false;

            GenerateStop();
        }

        public void ComputeMeshTerrainAreas()
        {
            float t = Time.realtimeSinceStartup;
            if (!GenerateStart()) return;

            for (int j = 0; j < area2D.meshTerrainAreas.Length; j++)
            {
                area2D.currentMeshTerrainArea = area2D.meshTerrainAreas[j];
                for (int i = 0; i < area2D.currentMeshTerrainArea.terrains.Count; i++)
                {
                    area2D.currentMeshTerrainArea.terrains[i].SetNodesActive(true);
                    // area2D.terrainLayer.GetItem(TC.heightOutput);
                    if (ComputeMeshTerrain(TC.heightOutput, area2D.currentMeshTerrainArea.terrains[i], false)) ++area2D.terrainsDone;
                    area2D.currentMeshTerrainArea.terrains[i].SetNodesActive(false);
                    // Debug.Log("MeshTerrain " + i);
                }
            }

            // isGeneratingHeight = false;

            GenerateStop();
            float f = 1 / (Time.realtimeSinceStartup - t);
            Debug.Log("Mesh Frames " + f);

        }

        public bool GenerateTerrain(TCUnityTerrain tcTerrain, int outputId, bool doGenerateStart = true)
        {
            if (tcTerrain == null) return false;

            if (doGenerateStart)
            {
                if (!GenerateStart()) return false;
            }
            
            // TC_TerrainLayer.current = area2D.terrainLayer;

            area2D.SetCurrentArea(tcTerrain, outputId);

            // Debug.Log(i);
            // Debug.Log("resolution " + resolution.x);
            // ReportArea();
            //a Debug.Log("Preview Resolution " + area2D.previewResolution);
            // area2D.terrainLayer.GetItem(outputId);

            if (tcTerrain.tasks > 0)
            {
                --tcTerrain.tasks;
                if (tcTerrain.tasks == 0)
                {
                    tcTerrain.terrain.Flush();
                    // Debug.Log("Flush terrain");

                    if (!tcTerrain.terrain.gameObject.activeSelf)
                    {
                        // tcTerrain.terrain.enabled = true;
                        tcTerrain.terrain.gameObject.SetActive(true);
                    }
                }
            }

            return true;
        }

        public void ReportArea()
        {
            Debug.Log("Resolution X " + area2D.resolution.x + " Y " + area2D.resolution.y);
            Debug.Log("IntResolution " + area2D.intResolution.ToString());
            Debug.Log("ResToTerrain X " + area2D.resolutionPM.x + " Y " + area2D.resolutionPM.y);

            Debug.Log("Bounds " + area2D.bounds);
            Debug.Log("StartPos X " + area2D.startPos.x + " Y " + area2D.startPos.y);
            Debug.Log("TerrainSize X " + area2D.terrainSize.x + " Y " + area2D.terrainSize.y);

            Debug.Log("Preview Resolution " + area2D.previewResolution);
            Debug.Log("ResToPreview X " + area2D.resToPreview.x + " Y " + area2D.resToPreview.y);

            Debug.Log("-------------------------------------------------------");
        }

        public bool ComputeMeshTerrain(int outputId, MeshTerrain tcMeshTerrain, bool doGenerateStart = true)
        {
            if (doGenerateStart)
            {
                if (!GenerateStart()) return false;
            }

            area2D.currentMeshTerrain = tcMeshTerrain;

            // if (!area2D.currentTCTerrain.active) return false;

            Int2 resolution = new Int2();
            Int2 resolution2 = new Int2();

            if (outputId == TC.heightOutput) { resolution.x = resolution.y = area2D.terrainLayer.meshResolution + 2; resolution2 = new Int2(resolution.x - 2, resolution.y - 2); }
            else if (outputId == TC.splatOutput) { resolution.x = resolution.y = area2D.terrainLayer.meshResolution; resolution2 = resolution; }
            // else if (computeGenerate.GetType() == typeof(ComputeGenerateTrees)) { resolution.x = resolution.y = area2D.layerLevel.meshResolution; resolution2 = resolution; }
            // else if (computeGenerate.GetType() == typeof(ComputeGenerateGrass)) { resolution.x = resolution.y = terrain.terrainData.detailResolution; resolution2 = resolution; }
            // else if (computeGenerate.GetType() == typeof(ComputeGenerateObjects)) { resolution.x = resolution.y = area2D.layerLevel.objectResolution; resolution2 = resolution; }
            else if (outputId == TC.colorOutput) { resolution.x = resolution.y = area2D.terrainLayer.meshResolution; resolution2 = resolution; }

            area2D.resolution = new Vector2(resolution.x, resolution.y);
            area2D.intResolution = resolution;
            TC_Reporter.Log("Resolution" + resolution.ToString());
            MeshTerrain meshTerrain = area2D.currentMeshTerrain;
            Vector2 size = new Vector2(meshTerrain.t.lossyScale.x * 10, meshTerrain.t.lossyScale.z * 10);
            area2D.resolutionPM = new Vector2(size.x / (resolution2.x), size.y / (resolution2.y));
            // Debug.Log("size " + size);
            // Debug.Log("con" + area2D.resToTerrain);
            area2D.area = new Rect(meshTerrain.t.position.x - (size.x / 2), meshTerrain.t.position.z - (size.y / 2), resolution.x, resolution.y);
            area2D.terrainSize = new Vector3(size.x, 4800, size.y);
            area2D.bounds = new Bounds(new Vector3(meshTerrain.t.position.x, 0, meshTerrain.t.position.z), area2D.terrainSize);
            area2D.startPos = new Vector3(area2D.area.xMin, meshTerrain.t.position.y, area2D.area.yMin);

            // Debug.Log(i);
            return true;
        }

        public void ComputeHeight()
        {
            TC_LayerGroup heightLayerGroup = area2D.terrainLayer.layerGroups[TC.heightOutput];
            // Debug.Log(area2D.currentTCUnityTerrain.terrain.name + " " + assignTerrainHeightmap);
            // Debug.Log("ComputeHeight");

            if (heightLayerGroup == null) return;
            if (!heightLayerGroup.active) return;

            if (!assignTerrainHeightmap) area2D.currentTCUnityTerrain.ResetObjects();

            TC_Compute compute = TC_Compute.instance;
            // isGeneratingHeight = true;
            int resolution = area2D.intResolution.x;

            ComputeBuffer buffer = null;

            // TC_Reporter.BenchmarkStart();
            heightLayerGroup.ComputeSingle(ref buffer, 0, true);
            // TC_Reporter.BenchmarkStop("Height compute");
            if (buffer == null) { TC_Reporter.Log("final buffer is null"); return; }

            // if (tcGenerate.isMesh) Debug.Log("Frames generate " + area2D.currentMeshTerrain.t.name + " " + f.ToString("F2"));
            // else 
            // Debug.Log("Frames generate " + area2D.currentTerrain.name + " " + f.ToString("F2"));
            // Debug.Log("Is mesh " + isMesh);

            if (!isMesh)
            {
                // Debug.Log(area2D.currentTCUnityTerrain.terrain.transform.name);
                // Debug.Log(((TCUnityTerrain)area2D.currentTCTerrain).terrain.name);

                compute.RunTerrainTex(buffer, ref area2D.currentTCTerrain.rtHeight, resolution);
                
                // else Debug.Log("Pass");
                
                RenderTexture rtHeight = area2D.currentTCTerrain.rtHeight;
                 
                TC_Compute.InitTexture(ref area2D.currentTCTerrain.texHeight, "HeightTexture "+area2D.currentTCUnityTerrain.terrain.name, rtHeight.width, true);
                // Debug.Log(area2D.currentTCTerrain.texHeight.mipmapCount);

                RenderTexture rtActiveOld = RenderTexture.active;
                RenderTexture.active = rtHeight;
                area2D.currentTCTerrain.texHeight.ReadPixels(new Rect(0, 0, rtHeight.width, rtHeight.height), 0, 0);
                area2D.currentTCTerrain.texHeight.Apply(); 
                RenderTexture.active = rtActiveOld;

                if (!assignTerrainHeightmap)
                {
                    // Debug.Log("!!!!!!!!!!!!!!");
                    compute.DisposeBuffer(ref buffer);
                    return;
                }

                int offset = area2D.resExpandBorder;

                int heightResolution = resolution - (offset * 2);

                TC.InitArray(ref heights, heightResolution, heightResolution);
                TC.InitArray(ref heightsReadback, resolution * resolution);
                // TODO: Can read directly into heights with rearranging the array
                buffer.GetData(heightsReadback);
                compute.DisposeBuffer(ref buffer);

                // Debug.Log(resolution);
                // Debug.Log(heightResolution);
                // Debug.Log(offset);

                //for (int x = 0; x < 32; x++)
                //{
                //    Int2 pixel = new Int2(x, 0);
                //    Color color = area2D.currentTCTerrain.texHeight.GetPixel(pixel.x, pixel.y + 1);

                //    float red = (color.r * 255) * 256;
                //    float green = (color.g * 255);

                //    float height = (red + green);

                //    float height2 = (heightsReadback[x] * 65535);

                //    Debug.Log(height2 + " - " + height + " = " + (height2 - height));
                //}

                for (int y = 0; y < heightResolution; y++)
                {
                    for (int x = 0; x < heightResolution; x++) heights[y, x] = heightsReadback[x + offset + ((y + offset) * resolution)];
                }

                if (area2D.currentTCUnityTerrain.tileX == 0 && area2D.currentTCUnityTerrain.tileZ == 0)
                {
                    area2D.currentTerrainArea.ResetNeighbors();
                }

                area2D.currentTerrain.terrainData.SetHeights(0, 0, heights);

                if (area2D.currentTCUnityTerrain.tileX == area2D.currentTerrainArea.tiles.x - 1 && area2D.currentTCUnityTerrain.tileZ == area2D.currentTerrainArea.tiles.y - 1)
                {
                    area2D.currentTerrainArea.SetNeighbors();
                }
                
                // area2D.currentTerrain.materialTemplate.SetTexture("_NormalMapGlobal", area2D.currentTCUnityTerrain.renderTex);
                // Debug.Log("Set heights");
            }
            else
            {
                // Debug.Log(area2D.currentMeshTerrain.t.name);
                // compute.RunTerrainTex(buffer, ref area2D.currentTCTerrain.rtHeight, true);
                // compute.DisposeBuffer(ref buffer);
                
                // Debug.Log(area2D.currentMeshTerrain.t.name);
                // Debug.Log("Assign RTP material");
            }
        }

        public void ComputeColor()
        {
            if (area2D.terrainLayer.layerGroups[TC.colorOutput] == null) return;

            TC_Compute compute = TC_Compute.instance;

            compute.SetPreviewColors(compute.colors);

            ComputeBuffer maskBuffer = null;

            TC_Compute.InitRenderTextures(ref compute.rtsColor, "rtsColor", 1);

            TC_Compute.InitRenderTexture(ref compute.rtResult, "rtResult");
            area2D.terrainLayer.layerGroups[TC.colorOutput].ComputeMulti(ref compute.rtsColor, ref maskBuffer, 0);

            area2D.currentTerrainArea.rtColormap = compute.rtsColor[0];

            if (maskBuffer != null) compute.DisposeBuffer(ref maskBuffer);

            if (!isMesh)
            {
                // compute.RunColormap(ref colorRTexture, ref area2D.currentTCUnityTerrain.colormap);

                RenderTexture rtActiveOld = RenderTexture.active;
                TC_Compute.InitTexture(ref area2D.currentTCUnityTerrain.texColormap, "texColormap", -1, true);
                Texture2D texColormap = area2D.currentTCUnityTerrain.texColormap;

                RenderTexture.active = area2D.currentTerrainArea.rtColormap;
                texColormap.ReadPixels(new Rect(0, 0, texColormap.width, texColormap.height), 0, 0);
                texColormap.Apply();

                RenderTexture.active = rtActiveOld;

                if (area2D.currentTCUnityTerrain.terrain.materialTemplate != null) area2D.currentTCUnityTerrain.terrain.materialTemplate.SetTexture("_Colormap", area2D.currentTCUnityTerrain.texColormap);
                else
                {
                    TC.AddMessage("The TC2 Colormap material is not assigned to the terrain. So it won't show.");
                    TC.AddMessage("This will be added in the next beta.");
                    // TC.AddMessage("Please go to the Setting tab in the inspector on Terrain Area GameObject and assign the custom material 'TC2_TerrainMaterial'.");
                    // TC.AddMessage("The 'TC2_TerrainMaterial' is in the folder TerrainComposer2 -> Shaders -> Terrain.");
#if UNITY_EDITOR
                    // UnityEditor.Selection.activeTransform = area2D.terrainAreas[0].transform;
#endif
                }

                // Material rtpMat = area2D.currentTerrain.materialTemplate;
                // rtpMat.SetTexture("_ColorMapGlobal", area2D.currentTCUnityTerrain.colormapTex);
                // area2D.currentTerrain.GetComponent<ReliefTerrain>().ColorGlobal = texColormap;
            }
            else
            {
                // compute.RunColormap(ref colorRTexture, ref area2D.currentMeshTerrain.colormap);
                area2D.currentMeshTerrain.rtpMat.SetTexture("_ColorMapGlobal", area2D.currentTerrainArea.rtColormap);
            }
        }

        public void ComputeSplat()
        {
            if (area2D.terrainLayer.layerGroups[TC.splatOutput] == null) return;
            int splatLength = area2D.splatmapLength;
            int splatmapLength = area2D.splatmapLength;

            // Debug.Log("Splat Compute " + TC_Area2D.current.currentTCUnityTerrain.terrain.name);

            if (splatLength == 0)
            {
                TC.AddMessage("No splat textures assigned to terrain '" + area2D.currentTerrain.name + "'");
                TC.AddMessage("Splat textures can be assigned on the Terrain Area GameObject -> Splat tab.", 2);
                return;
            }
            else if (splatLength > 8)
            {
                TC.AddMessage("At the moment TC2 only supports generating 8 splat textures. There are " + splatLength + " on " + area2D.currentTerrain.name + " assigned. In a later beta TC2 will be able to generate 16.", 0, 4);
                return;
            }

            TC_Compute compute = TC_Compute.instance;

            ComputeBuffer maskBuffer = null;
            RenderTexture[] rtSplatmaps;

            compute.SetPreviewColors(compute.splatColors);

            if (!isMesh)
            {
                TC_Compute.InitRenderTextures(ref area2D.currentTerrainArea.rtSplatmaps, "splatmapRTextures", splatmapLength);
            }
            else
            {
                // TCCompute.InitRenderTextures(ref area2D.currentTerrainArea.splatmapRTextures, "splatmapRTextures");
                // splatmapRTextures = area2D.currentTerrainArea.splatmapRTextures;
                // area2D.terrainTex = area2D.currentTerrainArea.renderTex;
            }
            TC_Compute.InitRenderTextures(ref compute.rtsResult, "resultRTextures");
            rtSplatmaps = area2D.currentTerrainArea.rtSplatmaps;

            area2D.terrainLayer.layerGroups[TC.splatOutput].ComputeMulti(ref rtSplatmaps, ref maskBuffer, 0);
            if (maskBuffer != null) compute.DisposeBuffer(ref maskBuffer);

            if (!isMesh)
            {
                Texture2D[] texSplatmaps = area2D.currentTerrain.terrainData.alphamapTextures;

                RenderTexture rtActiveOld = RenderTexture.active;
                // Debug.Log(area2D.currentTerrain.name + " " + splatmapRTextures.Length);
                for (int i = 0; i < rtSplatmaps.Length; i++)
                {
                    RenderTexture.active = rtSplatmaps[i];
                    texSplatmaps[i].ReadPixels(new Rect(0, 0, rtSplatmaps[i].width, rtSplatmaps[i].height), 0, 0);
                    texSplatmaps[i].Apply();
                }
                RenderTexture.active = rtActiveOld;
            }
            else
            {
                area2D.currentMeshTerrain.rtpMat.SetTexture("_Control1", rtSplatmaps[0]);
                area2D.currentMeshTerrain.rtpMat.SetTexture("_Control2", rtSplatmaps[1]);
                area2D.currentMeshTerrain.rtpMat.SetTexture("_Control3", rtSplatmaps[1]);
                // Debug.Log("Assign rtp splat");
            }
            // Debug.Log("Frames generate " + area2D.currentTerrain.name + " " + f.ToString("F2"));
        }

        // TODO: Same as splat
        public void ComputeGrass()
        {
            if (area2D.terrainLayer.layerGroups[TC.grassOutput] == null) return;
            int grassLength = area2D.currentTerrain.terrainData.detailPrototypes.Length;
            if (grassLength == 0)
            {
                TC.AddMessage("No grass assigned to terrain '" + area2D.currentTerrain.name + "'");
                TC.AddMessage("Grass can be assigned on the Terrain Area GameObject -> Grass tab.", 2);
                return;
            }
            else if (grassLength > 8)
            {
                TC.AddMessage("At the moment TC2 only supports generating 8 grass textures. There are " + grassLength + " on " + area2D.currentTerrain.name + " assigned. In a later beta TC2 will be able to generate 16.", 0, 4);
                return;
            }

            int resolution = area2D.intResolution.x;

            TC_Compute compute = TC_Compute.instance;
            compute.SetPreviewColors(compute.splatColors);
            
            ComputeBuffer maskBuffer = null;
            
            int grassCount = area2D.currentTerrain.terrainData.detailPrototypes.Length;
            int grassmapCount = Mathf.CeilToInt(grassCount / 4);
            grassmapCount = 2;

            // Debug.Log(grassmapCount);
            // Debug.Log(grassCount);
            // TC_Compute.InitRenderTextures(ref area2D.currentTerrainArea.rtSplatmaps, "splatmapRTextures");

            TC_Compute.InitRenderTextures(ref compute.rtsSplatmap, "rtsSplatmap", grassmapCount);
            TC_Compute.InitRenderTextures(ref compute.rtsResult, "rtsResult", grassmapCount);
            RenderTexture[] rtGrassmaps = compute.rtsSplatmap;

            area2D.terrainLayer.layerGroups[TC.grassOutput].ComputeMulti(ref rtGrassmaps, ref maskBuffer, 0);
            compute.DisposeBuffer(ref maskBuffer);

            TC_Compute.InitTextures(ref compute.texGrassmaps, "grassTextures", grassmapCount);
            compute.InitBytesArray(grassmapCount);
            TC_Compute.BytesArray[] bytesArray = compute.bytesArray;

            RenderTexture rtActiveOld = RenderTexture.active;
            for (int i = 0; i < rtGrassmaps.Length; i++)
            {
                RenderTexture.active = rtGrassmaps[i];
                compute.texGrassmaps[i].ReadPixels(new Rect(0, 0, rtGrassmaps[i].width, rtGrassmaps[i].height), 0, 0);
                bytesArray[i].bytes = compute.texGrassmaps[i].GetRawTextureData();
            }

            RenderTexture.active = rtActiveOld;

            TC.InitArray(ref grass, resolution, resolution);
            
            int index, colorIndex, byteIndex;

            for (int i = 0; i < grassCount; ++i)
            {
                index = i / 4;
                colorIndex = (1 + (i - (index * 4))) % 4;
                for (int y = 0; y < resolution; ++y)
                {
                    for (int x = 0; x < resolution; ++x)
                    {
                        byteIndex = (y * resolution * 4) + (x * 4) + colorIndex;
                        grass[y, x] = (int)(((float)bytesArray[index].bytes[byteIndex] / 255.0f) * 16.0f);
                    }
                }

                area2D.currentTerrain.terrainData.SetDetailLayer(0, 0, i, grass);
            }
        }

        public void ComputeTree()
        {
            if (area2D.terrainLayer.layerGroups[TC.treeOutput] == null) return;
            if (area2D.currentTerrain.terrainData.treePrototypes.Length == 0)
            {
                TC.AddMessage("No trees assigned to terrain '" + area2D.currentTerrain.name + "'");
                TC.AddMessage("Trees can be assigned on the Terrain Area GameObject -> Trees tab.", 2);
                return;
            }

            if (firstTreeTerrain == area2D.currentTCTerrain) { treesCount = 0; firstTreeTerrain = null; }
            
            int resolution = area2D.intResolution.x;

            TC_Compute compute = TC_Compute.instance;
            compute.SetPreviewColors(compute.splatColors);
            
            ComputeBuffer itemMapBuffer = null;

            area2D.terrainLayer.layerGroups[TC.treeOutput].ComputeSingle(ref itemMapBuffer, 0, true);
            compute.RunItemPositionCompute(itemMapBuffer, TC.treeOutput);

            ItemMap[] itemMap = new ItemMap[resolution * resolution];

            itemMapBuffer.GetData(itemMap);

            compute.DisposeBuffer(ref itemMapBuffer);

            if (trees == null) trees = new List<TreeInstance>();

            Vector3 terrainSize = area2D.currentTerrain.terrainData.size;
            Vector3 terrainPos = area2D.currentTerrain.transform.position;

            List<TC_SelectItem> treeItems = TC_Area2D.current.terrainLayer.treeSelectItems;
            
            for (int y = 0; y < resolution; ++y)
            {
                for (int x = 0; x < resolution; ++x)
                {
                    // TODO: Move more to compute shader
                    int index = (y * resolution) + x;
                    float density = itemMap[index].density;

                    if (density == 0) continue;

                    Vector3 pos = itemMap[index].pos;
                    
                    if (pos.x < 0 || pos.x > 1 || pos.z < 0 || pos.z > 1)
                    {
                        // Debug.Log(position.x + ", "+position.y+", "+position.z);
                        continue;
                    }

                    // Debug.Log("x " + itemMap[index].pos.x + " z " + itemMap[index].pos.z);

                    int id = itemMap[index].index;
                    if (id > treeItems.Count - 1)
                    {
                        TC.AddMessage("Tree index is out of bounds, index = " + id + ". Try the 'Refresh' button.");
                        return;
                    }

                    TC_SelectItem item = treeItems[id];
                    int treeIndex = item.selectIndex;
                    TC_SelectItem.Tree tree = item.tree;

                    Vector3 posSeed = new Vector3(pos.x * terrainSize.x, pos.y * terrainSize.y, pos.z * terrainSize.z) + terrainPos;// - item.t.parent.parent.position;
                    posSeed = Mathw.SnapVector3(posSeed + new Vector3(area2D.resolutionPM.x / 4, 0, area2D.resolutionPM.x / 4), area2D.resolutionPM.x / 2);

                    // pos.y += tree.heightOffset / terrainSize.y;

                    // Debug.Log(id + " " + treeIndex);

                    TreeInstance treeInstance = new TreeInstance();
                    treeInstance.color = Color.white;
                    treeInstance.lightmapColor = Color.white;

                    treeInstance.position = pos;
                    treeInstance.prototypeIndex = treeIndex;
                    #if UNITY_5_4_OR_NEWER
                    Random.InitState((int)posSeed.x + ((int)posSeed.z * resolution));
                    #else
                    Random.seed = (int)posSeed.x + ((int)posSeed.z * resolution);
                    #endif

                    treeInstance.rotation = RandomPos(new Vector2(pos.x, pos.z)) * 360;

                    Vector2 scaleRange = new Vector2(tree.scaleRange.x * item.parentItem.scaleMinMaxMulti.x, tree.scaleRange.y * item.parentItem.scaleMinMaxMulti.y);
                    float scaleRangeDelta = scaleRange.y - scaleRange.x;
                    if (scaleRangeDelta == 0) scaleRangeDelta = 0.001f;
                    treeInstance.heightScale = (tree.scaleCurve.Evaluate(Random.value) * scaleRangeDelta) + scaleRange.x;

                    float scaleMulti = tree.scaleMulti * item.parentItem.scaleMulti;

                    treeInstance.heightScale *= scaleMulti;
                    if (item.parentItem.linkScaleToMask) treeInstance.heightScale *= itemMap[index].maskValue;
                    if (treeInstance.heightScale < scaleRange.x * scaleMulti) treeInstance.heightScale = scaleRange.x * scaleMulti;
                    treeInstance.widthScale = treeInstance.heightScale * Random.Range(1 - tree.nonUniformScale, 1 + tree.nonUniformScale);
                    trees.Add(treeInstance);
                    ++treesCount;
                }
            }

            area2D.currentTerrain.terrainData.treeInstances = trees.ToArray();
            float[,] height1 = area2D.currentTerrain.terrainData.GetHeights(0, 0, 1, 1);
            area2D.currentTerrain.terrainData.SetHeights(0, 0, height1);
            trees.Clear();
        }

        public void ComputeObject()
        {
            if (objectParent != null) DestroyImmediate(objectParent);
            
            if (area2D.terrainLayer.layerGroups[TC.objectOutput] == null) return;
            if (area2D.terrainLayer.objectSelectItems.Count == 0)
            {
                TC.AddMessage("No objects nodes are active.");
                return;
            }

            if (firstObjectTerrain == area2D.currentTCTerrain) { objectsCount = 0; firstObjectTerrain = null; }

            int resolution = area2D.intResolution.x;

            Transform objectsParent = CheckObjectsParent(area2D.currentTCUnityTerrain);
            
            if (assignTerrainHeightmap) area2D.currentTCUnityTerrain.ResetObjects();
            
            
            TC_Compute compute = TC_Compute.instance;
            compute.SetPreviewColors(compute.splatColors);

            ComputeBuffer itemMapBuffer = null;
            area2D.terrainLayer.layerGroups[TC.objectOutput].ComputeSingle(ref itemMapBuffer, 0, true);
            compute.RunItemPositionCompute(itemMapBuffer, TC.objectOutput);

            ItemMap[] itemMap = new ItemMap[resolution * resolution];

            itemMapBuffer.GetData(itemMap);

            compute.DisposeBuffer(ref itemMapBuffer);

            // Vector2 terrainSize = area2D.area.size;
            // Vector2 terrainPos = area2D.area.position;
            Vector3 terrainSize = area2D.currentTerrain.terrainData.size;
            Vector3 terrainPos = area2D.currentTerrain.transform.position;

            // tcGenerate.ClearSpawnedObjects();
            // for (int i = 0; i < tcGenerate.objectItems.Length; i++) tcGenerate.objectItems[i].objectCount = 0;
            // Debug.Log(TCLayerLevel.current.objectitems.Count);
            // return;

            List<TC_SelectItem> objectItems = TC_Area2D.current.terrainLayer.objectSelectItems;
            
            for (int y = 0; y < resolution; ++y)
            {
                for (int x = 0; x < resolution; ++x)
                {
                    int index = (y * resolution) + x;
                    float density = itemMap[index].density;

                    if (density == 0) continue;

                    Vector3 pos = itemMap[index].pos;

                    if (pos.x < 0 || pos.x > 1 || pos.z < 0 || pos.z > 1)
                    {
                        // Debug.Log(position.x + ", "+position.y+", "+position.z);
                        continue;
                    }

                    // Debug.Log("x " + itemMap[index].pos.x + " z " + itemMap[index].pos.z);

                    int id = itemMap[index].index;
                    if (id > objectItems.Count - 1)
                    {
                        // Debug.Log("Object index is out of bounds, index = " + id);
                        TC.AddMessage("Object index is out of bounds, index = " + id + ". Try the 'Refresh' button.");
                        return;
                    }

                    TC_SelectItem item = objectItems[id];
                    TC_SelectItem.SpawnObject spawnObject = item.spawnObject;

                    pos = new Vector3(pos.x * terrainSize.x, pos.y * terrainSize.y, pos.z * terrainSize.z) + terrainPos;// - item.t.parent.parent.position;

                    if (!spawnObject.includeTerrainHeight) pos.y = 0;
                    Vector3 posSeed = Mathw.SnapVector3(pos + new Vector3(area2D.resolutionPM.x / 4, 0, area2D.resolutionPM.x / 4), area2D.resolutionPM.x / 2);

                    // Debug.Log((posSeed.x - pos.x) + ", " + (posSeed.z - pos.z));

                    #if UNITY_5_4_OR_NEWER
                    Random.InitState((int)posSeed.x + ((int)posSeed.z * resolution));
                    #else
                    Random.seed = (int)posSeed.x + ((int)posSeed.z * resolution);
                    #endif

                    Vector3 rotation = Vector3.zero;
                    if (spawnObject.lookAtTarget != null)
                    {
                        rotation = Quaternion.LookRotation(spawnObject.lookAtTarget.position - pos).eulerAngles;
                        if (!spawnObject.lookAtX) rotation.x = rotation.z = 0;
                    }
                    rotation += new Vector3(Random.Range(spawnObject.rotRangeX.x, spawnObject.rotRangeX.y), Random.Range(spawnObject.rotRangeY.x, spawnObject.rotRangeY.y), Random.Range(spawnObject.rotRangeZ.x, spawnObject.rotRangeZ.y));

                    if (spawnObject.isSnapRot)
                    {
                        if (spawnObject.isSnapRotX) rotation.x = ((int)(rotation.x / spawnObject.snapRotX)) * spawnObject.snapRotX;
                        if (spawnObject.isSnapRotY) rotation.y = ((int)(rotation.y / spawnObject.snapRotY)) * spawnObject.snapRotY;
                        if (spawnObject.isSnapRotZ) rotation.z = ((int)(rotation.z / spawnObject.snapRotZ)) * spawnObject.snapRotZ;
                    }

                    #if UNITY_5_4_OR_NEWER
                    Random.InitState((int)posSeed.x + ((int)posSeed.z * resolution));
                    #else
                    Random.seed = (int)posSeed.x + ((int)posSeed.z * resolution);
                    #endif

                    float scaleRangeDelta = spawnObject.scaleRange.y - spawnObject.scaleRange.x;
                    if (scaleRangeDelta == 0) scaleRangeDelta = 0.001f;

                    Vector3 scale;
                    scale.x = (spawnObject.scaleCurve.Evaluate(Random.value) * scaleRangeDelta) + spawnObject.scaleRange.x;

                    float scaleMulti = spawnObject.scaleMulti * item.parentItem.scaleMulti;

                    scale.x *= scaleMulti;
                    if (item.parentItem.linkScaleToMask) scale.x *= itemMap[index].maskValue;
                    if (scale.x < spawnObject.scaleRange.x * scaleMulti) scale.x = spawnObject.scaleRange.x * scaleMulti;
                    scale.y = scale.x * Random.Range(1 - spawnObject.nonUniformScale, 1 + spawnObject.nonUniformScale);
                    scale.z = scale.x * Random.Range(1 - spawnObject.nonUniformScale, 1 + spawnObject.nonUniformScale);

                    pos.y += spawnObject.heightOffset;
                    if (spawnObject.includeScale) pos.y += Random.Range(spawnObject.heightRange.x, spawnObject.heightRange.y) * scale.y;
                    else pos.y += Random.Range(spawnObject.heightRange.x, spawnObject.heightRange.y);

                    //if (tcObject.spawnList.Count <= tcGenerate.objectItems[objectIndex].objectCount)
                    //{
                    GameObject go;

#if !UNITY_EDITOR
                        go = (GameObject)Instantiate(spawnObject.go, pos, Quaternion.Euler(rotation));
#else
                        // TODO rotation can be returned as Quaternion
                        if (!spawnObject.linkToPrefab) go = (GameObject)Instantiate(spawnObject.go, pos, Quaternion.Euler(rotation));
                        else
                        {
                            go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(spawnObject.go);
                            go.transform.position = pos;
                            go.transform.rotation = Quaternion.Euler(rotation);
                        }
#endif

                    go.name = spawnObject.go.name;
                    go.transform.parent = objectsParent;
                    go.transform.localScale = Vector3.Scale(spawnObject.go.transform.localScale, scale);
                    ++objectsCount;
                    // tcObject.spawnList.Add(go.transform);
                    //}
                    //else
                    //{
                    //    tcObject.spawnList[tcGenerate.objectItems[objectIndex].objectCount].position = position;
                    //    tcObject.spawnList[tcGenerate.objectItems[objectIndex].objectCount].rotation = Quaternion.Euler(rotation);
                    //    tcObject.spawnList[tcGenerate.objectItems[objectIndex].objectCount].localScale = scale;
                    //    tcObject.spawnList[tcGenerate.objectItems[objectIndex].objectCount].gameObject.SetActive(true);
                    //}

                    // ++tcGenerate.objectItems[objectIndex].objectCount;
                    //}

                }
            }

            //for (int j = 0; j < tcGenerate.objectItems.Length; ++j)
            //{
            //    for (int i = tcGenerate.objectItems[j].objectCount; i < tcObjects[j].spawnList.Count; i++) tcObjects[j].spawnList[i].gameObject.SetActive(false);
            //}
        }

        public float RandomPos(Vector2 pos)
        {
            return Mathw.Frac(Mathf.Sin(Vector2.Dot(pos, new Vector2(12.9898f, 78.233f))) * 43758.5453123f);
        }

        public Transform CheckObjectsParent(TCUnityTerrain tcUnityTerrain)
        {
            if (tcUnityTerrain.objectsParent == null)
            {
                tcUnityTerrain.objectsParent = new GameObject("TerrainComposer Objects").transform;
                tcUnityTerrain.objectsParent.parent = tcUnityTerrain.terrain.transform;
            }

            return tcUnityTerrain.objectsParent;
        }

        struct ItemMap
        {
            public int index;
            public float density;
            public float maskValue;
            public Vector3 pos;
        };

        [System.Serializable]
        public class GenerateStackEntry
        {
            public List<GenerateStack> stack = new List<GenerateStack>();
            public int frame;

            public GenerateStackEntry(int frame)
            {
                this.frame = frame;
            }
        }
        
        [System.Serializable]
        public class GenerateStack
        {
            public TCUnityTerrain tcTerrain;
            public int outputId;
            public bool assignTerrainHeightmap;

            public GenerateStack(int outputId, TCUnityTerrain tcTerrain, bool assignTerrainHeightmap)
            {
                this.tcTerrain = tcTerrain;
                this.outputId = outputId;
                this.assignTerrainHeightmap = assignTerrainHeightmap;
            }
        }
    }
}