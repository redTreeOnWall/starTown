﻿using UnityEngine;
using System.Collections;
using System;
using System.IO;

namespace TerrainComposer2
{
	[ExecuteInEditMode]
	public class TC_RawImage : MonoBehaviour
	{
		public enum ByteOrder { Windows, Mac };

		public bool isResourcesFolder = false;
		public string path;
		public string filename;
		public int referenceCount;

		public Int2 resolution;
		public bool squareResolution;
		public ByteOrder byteOrder;
		public Texture2D tex;
		
		public bool isDestroyed, callDestroy;

		void Awake()
		{
			if (isDestroyed)
			{
				LoadRawImage(path);
				TC_Settings.instance.rawFiles.Add(this);
			}
			if (!callDestroy) { TC.refreshOutputReferences = TC.allOutput; referenceCount = 0; }
			else callDestroy = false;
		}

		void OnDestroy()
		{
			TC_Compute.DisposeTexture(ref tex);
			if (!callDestroy) TC.refreshOutputReferences = TC.allOutput;
		}

		void DestroyMe()
		{
			// Debug.Log("Destroy RawImage");
			TC_Settings settings = TC_Settings.instance;
			if (settings == null) return;
			if (settings.rawFiles == null) return;

			int index = settings.rawFiles.IndexOf(this);
			if (index != -1) settings.rawFiles.RemoveAt(index);
			
			#if UNITY_EDITOR
				UnityEditor.Undo.DestroyObjectImmediate(gameObject);
			#else
				Destroy(gameObject);
			#endif
		}

		public void UnregisterReference()
		{
			// Debug.Log(referenceCount);
			--referenceCount;
			// Debug.Log(referenceCount);
			if (referenceCount > 0) return;
			isDestroyed = true;
			callDestroy = true;

			DestroyMe();
		}

		public bool GetFileResolution()
		{
			if (!TC.FileExists(path)) return false;

			long length = TC.GetFileLength(path);

			GetResolutionFromLength(length);

			return true;
		}

		public void GetResolutionFromLength(long length)
		{
			float res = Mathf.Sqrt(length / 2);

			if (res == Mathf.Floor(res)) squareResolution = true; else squareResolution = false;

			resolution = new Int2(res, res);
		}

		public void LoadRawImage(string path)
		{
			this.path = path;

			string fullPath = Application.dataPath.Replace("Assets", "/") + path;
			// Debug.Log(fullPath);

			if (tex != null) return;

			#if UNITY_EDITOR
				if (!isResourcesFolder)
				{ 
					if (!TC.FileExists(fullPath)) return;
				}
			#endif

			TC_Reporter.Log("Load Raw file " + fullPath);

			// Debug.Log(bytes.Length);
			byte[] bytes = null;
			
			if (isResourcesFolder)
			{
				// Debug.Log("LoadRawImage " + path);
				TextAsset textAsset = Resources.Load<TextAsset>(path);
				if (textAsset != null) bytes = textAsset.bytes;
				else Debug.Log("Can't find file");
			}
			else
			{ 
				#if !UNITY_WEBPLAYER
					bytes = File.ReadAllBytes(fullPath);
				#else
					// TC.AddMessage("You are in Webplayer build mode, loading from disk is protected in this mode and stamp textures don't work.\nThis will be fixed.\n\nFor now another build mode in needed.", 0, 5); 
					WWW request = new WWW("file:///" + fullPath);

					while (!request.isDone) { }
					if (request.error != null) TC.AddMessage(request.error);

					bytes = request.bytes;
				#endif
			}
			
			if (bytes == null) return;
			if (bytes.Length == 0) return;

			GetResolutionFromLength(bytes.Length);
			tex = new Texture2D(resolution.x, resolution.y, TextureFormat.R16, false);
			tex.hideFlags = HideFlags.DontSave;
			tex.LoadRawTextureData(bytes);
			tex.Apply();
			
			// For use of mipmap
			//rt = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
			//rt.useMipMap = true;
			//rt.hideFlags = HideFlags.DontSave;
			//rt.Create();

			// Graphics.Blit(tex2, rt);
			// Debug.Log("Load");
		}
	}
}
