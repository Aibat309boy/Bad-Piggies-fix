using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
	public string AssetBundleName
	{
		get
		{
			return this.m_data.AssetBundle;
		}
	}

	public static bool IsLoadingLevel()
	{
		return LevelLoader.m_isLoadingLevel;
	}

	public string SceneName
	{
		get
		{
			return this.m_sceneName;
		}
	}

	public void SetSceneName(string sceneName)
	{
		this.m_sceneName = sceneName;
	}

	public void SetSingletonSpawner(GameObject singletonSpawner)
	{
		this.m_singletonSpawner = singletonSpawner;
	}

	public void AddPrefab(GameObject prefab)
	{
		this.m_prefabs.Add(prefab);
	}

	public void ClearPrefabs()
	{
		this.m_prefabs.Clear();
	}

	public void SetReferences(List<UnityEngine.Object> references)
	{
		this.m_references = references;
	}

	public Dictionary<GameObject, int> GetPrefabMapping()
	{
		Dictionary<GameObject, int> dictionary = new Dictionary<GameObject, int>();
		for (int i = 0; i < this.m_prefabs.Count; i++)
		{
			dictionary[this.m_prefabs[i]] = i;
		}
		return dictionary;
	}

	private void Awake()
	{
		if (this.m_singletonSpawner)
		{
			UnityEngine.Object.Instantiate<GameObject>(this.m_singletonSpawner);
		}
		RenderSettings.ambientLight = Color.white;
		TextAsset textAsset = this.m_data.LoadValue<TextAsset>();
		using (MemoryStream memoryStream = new MemoryStream(textAsset.bytes, false))
		{
			BinaryReader reader = new BinaryReader(memoryStream);
			LevelLoader.m_isLoadingLevel = true;
			this.ReadLevel(reader);
			LevelLoader.m_isLoadingLevel = false;
		}
		Resources.UnloadAsset(textAsset);
		base.gameObject.SetActive(false);
	}

	private void ReadLevel(BinaryReader reader)
	{
		int objectCount = reader.ReadInt32();
		for (int i = 0; i < objectCount; i++)
		{
			this.ReadObject(null, reader);
		}
	}

	private void ReadObject(GameObject parent, BinaryReader reader)
	{
		short childCount = reader.ReadInt16();
		if (childCount == 0)
		{
			this.ReadPrefabInstance(parent, reader);
		}
		else
		{
			this.ReadParentObject(childCount, parent, reader);
		}
	}

	private void ReadPrefabInstance(GameObject parent, BinaryReader reader)
	{
		string name = reader.ReadString();
		short index = reader.ReadInt16();
		GameObject original = this.m_prefabs[(int)index];
		Vector3 position = this.ReadVector3(reader);
		Vector3 euler = this.ReadVector3(reader);
		Vector3 localScale = this.ReadVector3(reader);
		GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(original);
		gameObject.name = name;
		if (parent != null)
		{
			gameObject.transform.parent = parent.transform;
		}
		gameObject.transform.position = position;
		gameObject.transform.rotation = Quaternion.Euler(euler);
		gameObject.transform.localScale = localScale;
		this.ReadData(gameObject, reader);
	}

	private void ReadParentObject(short childCount, GameObject parent, BinaryReader reader)
	{
		string name = reader.ReadString();
		Vector3 position = this.ReadVector3(reader);
		GameObject gameObject = new GameObject();
		gameObject.name = name;
		if (parent != null)
		{
			gameObject.transform.parent = parent.transform;
		}
		gameObject.transform.position = position;
		for (int i = 0; i < (int)childCount; i++)
		{
			this.ReadObject(gameObject, reader);
		}
	}

	private Vector2 ReadVector2(BinaryReader reader)
	{
		Vector2 result;
		result.x = reader.ReadSingle();
		result.y = reader.ReadSingle();
		return result;
	}

	private Vector3 ReadVector3(BinaryReader reader)
	{
		Vector3 result;
		result.x = reader.ReadSingle();
		result.y = reader.ReadSingle();
		result.z = reader.ReadSingle();
		return result;
	}

	private Color ReadColor(BinaryReader reader)
	{
		uint num = reader.ReadUInt32();
		Color result;
		result.r = (num >> 24 & 255U) * 0.003921569f;
		result.g = (num >> 16 & 255U) * 0.003921569f;
		result.b = (num >> 8 & 255U) * 0.003921569f;
		result.a = (num & 255U) * 0.003921569f;
		return result;
	}

	private void ReadData(GameObject obj, BinaryReader reader)
	{
		LevelLoader.DataType dataType = (LevelLoader.DataType)reader.ReadByte();
		if (dataType == LevelLoader.DataType.Terrain)
		{
			this.ReadTerrain(obj, reader);
		}
		else if (dataType == LevelLoader.DataType.PrefabOverrides)
		{
			this.ReadPrefabOverrides(obj, reader);
		}
	}

	private void ReadPrefabOverrides(GameObject obj, BinaryReader reader)
	{
		int count = reader.ReadInt32();
		byte[] buffer = new byte[count];
		reader.Read(buffer, 0, count);
		using (MemoryStream memoryStream = new MemoryStream(buffer))
		{
			using (StreamReader streamReader = new StreamReader(memoryStream, Encoding.UTF8))
			{
				ObjectDeserializer.ObjectReader reader2 = new ObjectDeserializer.ObjectReader(streamReader, this.m_references);
				ObjectDeserializer.ReadFile(obj, reader2);
			}
		}
		obj.SendMessage("OnDataLoaded", SendMessageOptions.DontRequireReceiver);
	}

	private void ReadTerrain(GameObject obj, BinaryReader reader)
	{
		e2dTerrain terrain = obj.GetComponent<e2dTerrain>();
		terrain.FillTextureTileOffsetX = reader.ReadSingle();
		terrain.FillTextureTileOffsetY = reader.ReadSingle();
		GameObject fill = obj.transform.Find("_fill").gameObject;
		MeshFilter filter = fill.GetComponent<MeshFilter>();
		Mesh sharedMesh = this.ReadMesh(false, true, false, reader, terrain);
		Color color = this.ReadColor(reader);
		int index = reader.ReadInt32();
		filter.sharedMesh = sharedMesh;
		fill.GetComponent<Renderer>().sharedMaterial.color = color;
		fill.GetComponent<Renderer>().sharedMaterial.mainTexture = (this.m_references[index] as Texture2D);
		GameObject curve = obj.transform.Find("_curve").gameObject;
		filter = curve.GetComponent<MeshFilter>();
		sharedMesh = this.ReadMesh(false, false, true, reader, terrain);
		filter.sharedMesh = sharedMesh;
		int curveTextureCount = reader.ReadInt32();
		for (int i = 0; i < curveTextureCount; i++)
		{
			if (i >= terrain.CurveTextures.Count)
			{
				terrain.CurveTextures.Add(new e2dCurveTexture(null));
			}
			int index2 = reader.ReadInt32();
			terrain.CurveTextures[i].texture = (this.m_references[index2] as Texture);
			terrain.CurveTextures[i].size = this.ReadVector2(reader);
			terrain.CurveTextures[i].fixedAngle = reader.ReadBoolean();
			terrain.CurveTextures[i].fadeThreshold = reader.ReadSingle();
		}
		int hasControlTexture = reader.ReadInt32();
		if (hasControlTexture > 0)
		{
			int count = reader.ReadInt32();
			byte[] data = reader.ReadBytes(count);
			Texture2D texture2D = new Texture2D(1, 1, TextureFormat.ARGB32, false);
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.anisoLevel = 1;
			if (!texture2D.LoadImage(data))
			{
				throw new InvalidOperationException("Can't load control texture");
			}
			terrain.CurveMesh.ControlTextures.Clear();
			terrain.CurveMesh.ControlTextures.Add(texture2D);
			terrain.CurveMesh.RebuildMaterial();
		}
		bool hasCollision = reader.ReadBoolean();
		if (hasCollision)
		{
			filter = fill.GetComponent<MeshFilter>();
			Mesh sharedMesh2 = filter.sharedMesh;
			this.CreateCollider(obj, sharedMesh2.vertices);
		}
		else if (obj.transform.Find(e2dConstants.COLLIDER_MESH_NAME))
		{
			UnityEngine.Object.Destroy(obj.transform.Find(e2dConstants.COLLIDER_MESH_NAME).gameObject);
		}
	}

	private Mesh ReadMesh(bool readUV, bool fillMesh, bool readColor, BinaryReader reader, e2dTerrain terrain)
	{
		Mesh mesh = new Mesh();
		int count = reader.ReadInt32();
		Vector3[] vertices = new Vector3[count];
		if (fillMesh)
		{
			for (int i = 0; i < vertices.Length; i++)
			{
				vertices[i] = this.ReadVector2(reader);
			}
			Vector2[] uv = new Vector2[vertices.Length];
			float num2 = 1f / terrain.FillTextureTileWidth;
			float num3 = 1f / terrain.FillTextureTileHeight;
			for (int j = 0; j < uv.Length; j++)
			{
				uv[j].x = (vertices[j].x - terrain.FillTextureTileOffsetX) * num2;
				uv[j].y = (vertices[j].y - terrain.FillTextureTileOffsetY) * num3;
			}
			mesh.vertices = vertices;
			mesh.uv = uv;
		}
		else
		{
			Color[] colors = new Color[vertices.Length];
			for (int k = 0; k < vertices.Length; k++)
			{
				vertices[k] = this.ReadVector2(reader);
				vertices[k].z = -0.01f;
				colors[k].r = (float)((k + 1) % 2);
			}
			Vector2[] uv = new Vector2[vertices.Length];
			int num4 = count / 2;
			float num5 = 0f;
			for (int l = 1; l < num4; l++)
			{
				int num6 = l;
				int num7 = 2 * num6;
				float magnitude = (vertices[num7] - vertices[num7 - 2]).magnitude;
				num5 += magnitude;
				uv[num7] = (uv[num7 + 1] = new Vector2(num5, (float)num6));
			}
			mesh.vertices = vertices;
			mesh.colors = colors;
			mesh.uv = uv;
		}
		if (readUV)
		{
			int uvCount = reader.ReadInt32();
			Vector2[] uv = new Vector2[uvCount];
			for (int m = 0; m < uv.Length; m++)
			{
				uv[m] = this.ReadVector2(reader);
			}
			mesh.uv = uv;
		}
		int triangleCount = reader.ReadInt32();
		int[] triangles = new int[triangleCount];
		for (int n = 0; n < triangleCount; n++)
		{
			triangles[n] = (int)reader.ReadInt16();
		}
		mesh.triangles = triangles;
		return mesh;
	}

	private Vector2 GetPointFillUV(e2dTerrain terrain, Vector2 curvePoint)
	{
		float x = (curvePoint.x - terrain.FillTextureTileOffsetX) / terrain.FillTextureTileWidth;
		float y = (curvePoint.y - terrain.FillTextureTileOffsetY) / terrain.FillTextureTileHeight;
		return new Vector2(x, y);
	}

	public void CreateCollider(GameObject obj, Vector3[] polygon)
	{
		Vector3[] vertices = new Vector3[2 * polygon.Length];
		int[] triangles = new int[6 * polygon.Length];
		for (int i = 0; i < polygon.Length; i++)
		{
			int num = 2 * i;
			vertices[num] = new Vector3(polygon[i].x, polygon[i].y, -0.5f * e2dConstants.COLLISION_MESH_Z_DEPTH);
			vertices[num + 1] = new Vector3(polygon[i].x, polygon[i].y, 0.5f * e2dConstants.COLLISION_MESH_Z_DEPTH);
			int num2 = 6 * i;
			triangles[num2] = num % vertices.Length;
			triangles[num2 + 1] = (num + 1) % vertices.Length;
			triangles[num2 + 2] = (num + 2) % vertices.Length;
			triangles[num2 + 3] = (num + 2) % vertices.Length;
			triangles[num2 + 4] = (num + 1) % vertices.Length;
			triangles[num2 + 5] = (num + 3) % vertices.Length;
		}
		Transform transform = obj.transform.Find(e2dConstants.COLLIDER_MESH_NAME);
		MeshCollider collider = transform.GetComponent<MeshCollider>();
		collider.sharedMesh = new Mesh
		{
			vertices = vertices,
			triangles = triangles
		};
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
		transform.localScale = Vector3.one;
		if (obj.layer != LayerMask.NameToLayer("IceGround"))
		{
			collider.gameObject.layer = LayerMask.NameToLayer("Ground");
		}
	}

	public override string ToString()
	{
		return this.SceneName;
	}

	[SerializeField]
	private string m_sceneName;

	[SerializeField]
	private GameObject m_singletonSpawner;

	[SerializeField]
	private List<GameObject> m_prefabs = new List<GameObject>();

	[SerializeField]
	private List<UnityEngine.Object> m_references = new List<UnityEngine.Object>();

	[SerializeField]
	private BundleDataObject m_data;

	private static bool m_isLoadingLevel;

	public enum DataType
	{
		None,
		Terrain,
		PrefabOverrides
	}
}
