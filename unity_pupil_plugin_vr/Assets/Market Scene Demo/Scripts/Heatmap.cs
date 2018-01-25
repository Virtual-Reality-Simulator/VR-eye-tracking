﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using Pupil;
using FFmpegOut;

public class Heatmap : MonoBehaviour 
{
	[Range(0.125f,1f)]
	public float sizeOfElement = 1;
	public float removeAfterTimeInterval = 10;

	public enum HeatmapMode
	{
		Particle,
		ParticleDebug,
		Highlight,
		Image
	}
	public HeatmapMode mode;
	public Color particleColor;
	public Color particleColorFinal;

	public TextMesh infoText;

	LayerMask collisionLayer;
	EStatus previousEStatus;

	Camera cam;
	public Camera RenderingCamera;
	public Camera MaskingCamera;
	// Use this for initialization
	void OnEnable () 
	{
		if (PupilTools.IsConnected)
		{
			if (PupilTools.DataProcessState != EStatus.ProcessingGaze)
			{
				previousEStatus = PupilTools.DataProcessState;
				PupilTools.DataProcessState = EStatus.ProcessingGaze;
				PupilTools.SubscribeTo ("gaze");
			}
		}

		cam = GetComponentInParent<Camera> ();

		transform.localPosition = Vector3.zero;

		collisionLayer = (1 << LayerMask.NameToLayer ("HeatmapMesh"));

		InitializeMeshes ();

		switch (mode)
		{
		case HeatmapMode.Highlight:
			if (MaskingCamera != null)
				MaskingCamera.backgroundColor = Color.white;
			particleSystemParameters.startColor = Color.black;
			particleSystemParameters.startSize = sizeOfElement * 0.1f;
			particleSystemParameters.startLifetime = removeAfterTimeInterval;
			break;
		case HeatmapMode.ParticleDebug:
			particleSystemParameters.startColor = particleColor;
			particleSystemParameters.startSize = sizeOfElement * 0.05f;
			particleSystemParameters.startLifetime = removeAfterTimeInterval;
			currentVisualization.gameObject.layer = 0;
			break;
		case HeatmapMode.Image:
			particleSystemParameters.startColor = particleColor;
			particleSystemParameters.startSize = sizeOfElement * 0.033f;
			particleSystemParameters.startLifetime = float.MaxValue;
			visualizationParticles = new ParticleSystem.Particle[currentVisualization.main.maxParticles];
			break;
		default:
			particleSystemParameters.startColor = particleColor;
			particleSystemParameters.startSize = sizeOfElement * 0.033f;
			particleSystemParameters.startLifetime = removeAfterTimeInterval;
			break;
		}
	}

	void OnDisable()
	{
		if (previousEStatus != EStatus.ProcessingGaze)
		{
			PupilTools.DataProcessState = previousEStatus;
			PupilTools.UnSubscribeFrom ("gaze");
		}

		if ( _pipe != null)
			ClosePipe ();
	}

	private RenderTexture _cubemap;
	public RenderTexture Cubemap
	{
		get
		{
			if (_cubemap == null)
			{
				_cubemap = new RenderTexture (2048, 2048, 0);
				_cubemap.dimension = UnityEngine.Rendering.TextureDimension.Cube;
				_cubemap.enableRandomWrite = true;
				_cubemap.Create ();
			}
			return _cubemap;
		}
	}

	MeshFilter RenderingMeshFilter;
	Material renderingMaterial;
	RenderTexture renderingTexture;
	RenderTexture maskingTexture;
	void InitializeMeshes()
	{
		var sphereMesh = GetComponent<MeshFilter> ().mesh;
		if (sphereMesh.triangles [0] == 0)
		{
			sphereMesh.triangles = sphereMesh.triangles.Reverse ().ToArray ();
		}
		gameObject.AddComponent<MeshCollider> ();
		currentVisualization = GetComponentInChildren<ParticleSystem> ();
		visualizations = new List<ParticleSystem> ();
		visualizations.Add (currentVisualization);

		if (RenderingCamera != null)
		{
			RenderingCamera.aspect = 2;
			renderingTexture = new RenderTexture (2048, 1024, 0);
			RenderingCamera.targetTexture = renderingTexture;

			RenderingMeshFilter = RenderingCamera.GetComponentInChildren<MeshFilter> ();
			RenderingMeshFilter.mesh = GeneratePlaneWithSphereNormals ();
			renderingMaterial = RenderingCamera.GetComponentInChildren<MeshRenderer> ().material;
			renderingMaterial.SetTexture ("_Cubemap", Cubemap);

			RenderingCamera.transform.parent = null;

			if (MaskingCamera != null)
			{
				MaskingCamera.aspect = 2;
				maskingTexture = new RenderTexture (2048, 1024, 0);
				MaskingCamera.targetTexture = maskingTexture;
				renderingMaterial.SetTexture ("_Mask", maskingTexture);
			}
		}
	}

	int sphereMeshHeight = 32;
	int sphereMeshWidth = 32;
	Vector2 sphereMeshCenterOffset = Vector2.one * 0.5f;
	Mesh GeneratePlaneWithSphereNormals()
	{
		Mesh result = new Mesh ();

		var vertices = new Vector3[sphereMeshHeight * sphereMeshWidth];
		var uvs = new Vector2[sphereMeshHeight * sphereMeshWidth];

		List<int> triangles = new List<int> ();

		for (int i = 0; i < sphereMeshHeight; i++)
		{
			for (int j = 0; j < sphereMeshWidth; j++)
			{
				Vector2 uv = new Vector2 ((float)j / (float)(sphereMeshWidth - 1), (float)i / (float)(sphereMeshHeight - 1));
				uvs [j + i * sphereMeshWidth] = new Vector2(1f - uv.x, 1f - uv.y);
				vertices [j + i * sphereMeshWidth] = PositionForUV(uv);

				if (i > 0 && j > 0)
				{
					triangles.Add (j + i * sphereMeshWidth);
					triangles.Add ((j - 1) + (i - 1) * sphereMeshWidth);
					triangles.Add ((j - 1) + i * sphereMeshWidth);
					triangles.Add (j + (i - 1) * sphereMeshWidth);
					triangles.Add ((j - 1) + (i - 1) * sphereMeshWidth);
					triangles.Add (j + i * sphereMeshWidth);
				}
			}
		}
		result.vertices = vertices;
		result.uv = uvs;
		result.triangles = triangles.ToArray ();
		result.RecalculateNormals ();

		return result;
	}

	Vector3 PositionForUV (Vector2 uv)
	{
		Vector2 position = uv;
		position -= sphereMeshCenterOffset;
		position.x *= RenderingCamera.aspect;
		position.y *= RenderingCamera.orthographicSize * 2f;
		return position;
	}

	ParticleSystem currentVisualization;
	List<ParticleSystem> visualizations;
	ParticleSystem.Particle[] visualizationParticles;
	ParticleSystem.EmitParams particleSystemParameters = new ParticleSystem.EmitParams ();
	void Add(Vector3 point)
	{
		particleSystemParameters.position = point;
		currentVisualization.Emit (particleSystemParameters, 1);
		if (currentVisualization.particleCount == currentVisualization.main.maxParticles)
		{
			Debug.Log ("Approaching maximum number of particles. Will instantiate additional particle system. If this continues to occur, please increase the number in Unity Editor");
			var newViz = GameObject.Instantiate<ParticleSystem> (currentVisualization, currentVisualization.transform);
			newViz.Clear ();
			visualizations.Add (newViz);
			currentVisualization = newViz;
		}

		if (mode == HeatmapMode.Image)
		{
			int numberOfOverallParticles = 0;
			foreach (var visualization in visualizations)
			{
				numberOfOverallParticles += visualization.particleCount;
			}
			int currentOverallParticleCounter = 0;
			for (int i = 0; i < visualizations.Count; i++)
			{
				var visualization = visualizations [i];
				int numberOfParticlesAlive = visualization.GetParticles (visualizationParticles);
				for (int j = 0; j < numberOfParticlesAlive; j++)
				{
					visualizationParticles [j].startColor = Color32.Lerp (particleColorFinal, particleColor, (float)(currentOverallParticleCounter) / (float)(numberOfOverallParticles - 1));
					currentOverallParticleCounter += 1;
				}
				visualization.SetParticles (visualizationParticles, numberOfParticlesAlive);
			}
		}
	}

	void Update () 
	{
		transform.eulerAngles = Vector3.zero;

		if (PupilTools.IsConnected && PupilTools.DataProcessState == EStatus.ProcessingGaze)
		{
			Vector2 gazePosition = PupilData._2D.GetEyeGaze (GazeSource.BothEyes);

			RaycastHit hit;
//			if (Input.GetMouseButton(0) && Physics.Raycast(cam.ScreenPointToRay (Input.mousePosition), out hit, 1f, (int) collisionLayer))
			if (Physics.Raycast(cam.ViewportPointToRay (gazePosition), out hit, 1f, (int)collisionLayer))
			{
				if ( hit.collider.gameObject != gameObject )
					return;

				if (mode == HeatmapMode.ParticleDebug)
					Add (hit.point);
				else
					Add (RenderingMeshFilter.transform.localToWorldMatrix.MultiplyPoint3x4 (PositionForUV (Vector2.one - hit.textureCoord) - Vector3.forward * 0.001f));
			}
		}

		if ( renderingMaterial != null)
			cam.RenderToCubemap (Cubemap);
		
		if (Input.GetKeyUp (KeyCode.H))
		{
			capturing = !capturing;
			if (infoText != null && infoText.gameObject.activeInHierarchy)
				infoText.gameObject.SetActive (false);
		}
				
		
	}

	void LateUpdate()
	{
		if (capturing)
		{
			if (mode == HeatmapMode.Image)
			{
				string path = PupilSettings.Instance.recorder.GetRecordingPath ();
				System.IO.File.WriteAllBytes (path + string.Format ("/Heatmap_{0}.jpg", Time.time), CaptureCurrentView ().EncodeToJPG ());
				capturing = false;
			}
			else
			{
				if (_pipe == null)
					OpenPipe ();
				else
				{
					// With the winter 2017 release of this plugin, Pupil timestamp is set to Unity time when connecting
					timeStampList.Add (Time.time);
					_pipe.Write (CaptureCurrentView ().GetRawTextureData ());
				}
			}
		} else
		{
			if (_pipe != null)
				ClosePipe ();
		}
	}

	bool capturing = false;
	RenderTexture previouslyActiveRenderTexture;
	Texture2D temporaryTexture;
	Texture2D CaptureCurrentView()
	{
		previouslyActiveRenderTexture = RenderTexture.active;
		RenderTexture.active = renderingTexture;
		if (temporaryTexture == null)
		{
			temporaryTexture = new Texture2D (renderingTexture.width, renderingTexture.height, TextureFormat.RGB24, false);
		}
		temporaryTexture.ReadPixels (new Rect (0, 0, renderingTexture.width, renderingTexture.height), 0, 0, false);
		temporaryTexture.Apply ();

		RenderTexture.active = previouslyActiveRenderTexture;

		return temporaryTexture;
	}

	FFmpegPipe _pipe;
	List<double> timeStampList = new List<double>();
	int _frameRate = 30;

	void OpenPipe()
	{
		timeStampList = new List<double> ();

		// Open an output stream.
		_pipe = new FFmpegPipe("Heatmap", renderingTexture.width, renderingTexture.height, _frameRate, PupilSettings.Instance.recorder.codec);

		Debug.Log("Capture started (" + _pipe.Filename + ")");
	}

	void ClosePipe()
	{
		// Close the output stream.
		Debug.Log ("Capture ended (" + _pipe.Filename + ").");

		// Write pupil timestamps to a file
		string timeStampFileName = "Heatmap_Timestamps";
		byte[] timeStampByteArray = PupilConversions.doubleArrayToByteArray (timeStampList.ToArray ());
		File.WriteAllBytes(_pipe.FilePath + "/" + timeStampFileName + ".time", timeStampByteArray);

		_pipe.Close();

		if (!string.IsNullOrEmpty(_pipe.Error))
		{
			Debug.LogWarning(
				"ffmpeg returned with a warning or an error message. " +
				"See the following lines for details:\n" + _pipe.Error
			);
		}

		_pipe = null;

		if (infoText != null && !infoText.gameObject.activeInHierarchy)
			infoText.gameObject.SetActive (true);
	}
}