using System.Collections.Generic;
using UnityEngine;
using AnEchoHasNoShape.Core;

namespace AnEchoHasNoShape.Echolocation
{
    [DisallowMultipleComponent]
    public sealed class WoodsEchoResponse : MonoBehaviour
    {
        public static WoodsEchoResponse Instance { get; private set; }
        [SerializeField, Min(0f)] private float replyDelay = 3f;
        [SerializeField, Min(1f)] private float speed = 35f;
        [SerializeField, Min(1f)] private float range = 400f;
        [SerializeField, Min(0.1f)] private float holdSeconds = 5f;
        [SerializeField, Min(0.1f)] private float fadeSeconds = 2f;
        [SerializeField] private Color replyColor = new Color(0.55f, 0.85f, 0.62f);
        [SerializeField] private AudioClip replyClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
        private readonly List<Collider> innerBounds = new();
        private readonly Vector4[] overwriteOrigins = new Vector4[32];
        private readonly Vector4[] overwriteSettings = new Vector4[32];
        private int overwriteCount;
        private float due = -1f, started = -1f;
        private float replyEndTime;
        private Transform player;
        private AudioSource audioSource;
        private float audioDuck = 1f;
        private bool hidden;
        private GameObject falseWalls;
        private bool falseWallsWereActive;
        private float disableFalseWallsAt = -1f;
        private float completionStarted = -1f, completionSpeed, nextRemoval;
        private Vector3 completionOrigin;
        private sealed class PendingTerrain
        {
            public Terrain terrain;
            public TerrainData data;
            public HashSet<int> indexes;
            public List<TreeInstance> trees;
        }
        private readonly List<PendingTerrain> pendingTerrains = new();
        private readonly List<(Renderer renderer, bool enabled)> hiddenRenderers = new();
        private readonly List<(Terrain terrain, TerrainData original, TerrainData copy, TerrainCollider collider)> copies = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Instance = null;
            Shader.SetGlobalFloat("_GrassWoodsEchoValid", 0f);
            Shader.SetGlobalFloat("_WoodsReplyActive", 0f);
            Shader.SetGlobalInt("_WoodsOverwriteCount", 0);
            Shader.SetGlobalFloat("_WoodsCompletionActive", 0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            GameObject source = GameObject.Find("WoodsEchoSource");
            if (source != null && source.GetComponent<WoodsEchoResponse>() == null)
                source.AddComponent<WoodsEchoResponse>();
        }

        private void Awake()
        {
            Instance = this;
            foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsInactive.Include))
                if (collider.name.StartsWith("WoodsInnerBounds", System.StringComparison.OrdinalIgnoreCase)) innerBounds.Add(collider);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 10f;
            audioSource.maxDistance = range;
            audioSource.dopplerLevel = 0f;
        }

        private bool Inside(Vector3 position)
        {
            foreach (Collider bounds in innerBounds)
                if (bounds != null && bounds.enabled && bounds.gameObject.activeInHierarchy &&
                    (bounds.ClosestPoint(position) - position).sqrMagnitude < 0.000001f) return true;
            return false;
        }

        public void OnPlayerEcho(Vector3 position, float pulseSpeed, float pulseRange, float width, Transform playerTransform)
        {
            player = playerTransform;
            if (started >= 0f && overwriteCount < overwriteOrigins.Length)
            {
                overwriteOrigins[overwriteCount] = new Vector4(position.x, position.y, position.z, Time.time);
                overwriteSettings[overwriteCount++] = new Vector4(pulseSpeed, pulseRange, Mathf.Max(width, 1f), 0f);
                Shader.SetGlobalVectorArray("_WoodsOverwriteOrigins", overwriteOrigins);
                Shader.SetGlobalVectorArray("_WoodsOverwriteSettings", overwriteSettings);
                Shader.SetGlobalInt("_WoodsOverwriteCount", overwriteCount);
                audioDuck = 0f;
            }
            due = !hidden && Inside(player.position) ? Time.time + replyDelay : -1f;
        }

        private void Update()
        {
            if (disableFalseWallsAt >= 0f && Time.time >= disableFalseWallsAt)
            {
                disableFalseWallsAt = -1f;
                if (falseWalls != null) falseWalls.SetActive(false);
            }
            UpdateCompletion();
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, volume * audioDuck, Time.deltaTime * 2f);
            if (due >= 0f && (player == null || !Inside(player.position))) due = -1f;
            if (due >= 0f && Time.time >= due)
            {
                due = -1f;
                started = Time.time;
                overwriteCount = 0;
                Shader.SetGlobalInt("_WoodsOverwriteCount", 0);
                Shader.SetGlobalVector("_WoodsReplyOrigin", transform.position);
                Shader.SetGlobalVector("_WoodsReplySettings", new Vector4(started, Mathf.Max(1f, speed), range, holdSeconds));
                Shader.SetGlobalFloat("_WoodsReplyFade", Mathf.Max(0.1f, fadeSeconds));
                Shader.SetGlobalColor("_WoodsReplyColor", replyColor);
                Shader.SetGlobalVector("_WoodsReplyStyle", new Vector4(
                    Mathf.Max(0.01f, Shader.GetGlobalFloat("_EchoPulseWidth")),
                    Mathf.Max(0.01f, Shader.GetGlobalFloat("_EchoTrailLength")),
                    Mathf.Max(0f, Shader.GetGlobalFloat("_EchoPulseIntensity")),
                    Mathf.Clamp(Shader.GetGlobalFloat("_EchoPulseCount"), 1f, 3f)));
                Shader.SetGlobalFloat("_WoodsReplySpacing", Shader.GetGlobalFloat("_EchoPulseSpacing"));
                replyEndTime = started + (range + Shader.GetGlobalFloat("_EchoPulseSpacing") * 2.35f)
                    / Mathf.Max(1f, speed) + holdSeconds + fadeSeconds;
                Shader.SetGlobalFloat("_WoodsReplyActive", 1f);
                Shader.SetGlobalFloat("_GrassWoodsEchoValid", 1f);
                audioDuck = 1f;
                audioSource.volume = volume;
                AudioClip clip = replyClip != null ? replyClip : GameAudioLibrary.Load()?.EchoClip;
                audioSource.clip = clip;
                audioSource.pitch = 0.9f;
                if (clip != null) audioSource.Play();
            }
            if (started >= 0f && Time.time > replyEndTime)
            {
                started = -1f;
                Shader.SetGlobalFloat("_WoodsReplyActive", 0f);
            }
        }

        public void HideIllusions(bool animate = true)
        {
            if (hidden) return;
            hidden = true;
            due = started = -1f;
            Shader.SetGlobalFloat("_GrassWoodsEchoValid", 0f);
            Shader.SetGlobalFloat("_WoodsReplyActive", 0f);
            audioDuck = 0f;
            var world = WorldReverberationController.Instance;
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (!candidate.name.Equals("FalseWalls", System.StringComparison.OrdinalIgnoreCase)) continue;
                falseWalls = candidate.gameObject;
                falseWallsWereActive = falseWalls.activeSelf;
                break;
            }
            if (falseWalls != null)
            {
                if (animate && world != null) disableFalseWallsAt = Time.time + world.PulseDuration;
                else falseWalls.SetActive(false);
            }
            completionOrigin = world != null ? world.transform.position : transform.position;
            completionSpeed = world != null ? world.PulseSpeed : 52f;
            completionStarted = Time.time;
            Shader.SetGlobalVector("_WoodsCompletionOrigin", completionOrigin);
            Shader.SetGlobalVector("_WoodsCompletionWave", new Vector4(completionStarted, completionSpeed, animate ? 0f : 1f, 0f));
            Shader.SetGlobalFloat("_WoodsCompletionActive", 1f);
            var seen = new HashSet<Renderer>();
            foreach (WoodsIllusionTree marker in FindObjectsByType<WoodsIllusionTree>(FindObjectsInactive.Include))
            {
                if (!marker.enabled) continue;
                foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>(true))
                    if (seen.Add(renderer)) hiddenRenderers.Add((renderer, renderer.enabled));
            }
            foreach (Terrain terrain in FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            {
                TerrainData original = terrain.terrainData;
                if (original == null) continue;
                var indexes = new HashSet<int>();
                TreePrototype[] prototypes = original.treePrototypes;
                for (int i = 0; i < prototypes.Length; i++)
                    if (prototypes[i].prefab != null && WoodsIllusionTree.IsIllusion(prototypes[i].prefab.transform)) indexes.Add(i);
                if (indexes.Count == 0) continue;
                TreeInstance[] originalTrees = original.treeInstances;
                if (!System.Array.Exists(originalTrees, tree => indexes.Contains(tree.prototypeIndex))) continue;
                TerrainData copy = Instantiate(original);
                copy.name = original.name + " Woods Runtime";
                copy.hideFlags = HideFlags.DontSave;
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider != null && collider.terrainData != original) collider = null;
                copies.Add((terrain, original, copy, collider));
                terrain.terrainData = copy;
                if (collider != null) collider.terrainData = copy;
                terrain.Flush();
                pendingTerrains.Add(new PendingTerrain { terrain = terrain, data = copy,
                    indexes = indexes, trees = new List<TreeInstance>(originalTrees) });
            }
            UpdateCompletion(!animate);
        }

        private void UpdateCompletion(bool immediate = false)
        {
            if (completionStarted < 0f || (!immediate && Time.time < nextRemoval)) return;
            nextRemoval = Time.time + 0.1f;
            float radius = immediate ? float.PositiveInfinity : (Time.time - completionStarted) * completionSpeed;
            for (int terrainIndex = pendingTerrains.Count - 1; terrainIndex >= 0; terrainIndex--)
            {
                var entry = pendingTerrains[terrainIndex];
                if (entry.terrain == null || entry.data == null) { pendingTerrains.RemoveAt(terrainIndex); continue; }
                // Compact the cached list in place; do not copy TerrainData's full
                // native tree array and allocate a new list on every wave tick.
                List<TreeInstance> trees = entry.trees;
                int remainingCount = 0;
                bool hasIllusions = false;
                Vector3 terrainPosition = entry.terrain.transform.position;
                Vector3 terrainSize = entry.data.size;
                float radiusSquared = radius * radius;
                for (int treeIndex = 0; treeIndex < trees.Count; treeIndex++)
                {
                    TreeInstance tree = trees[treeIndex];
                    bool illusion = entry.indexes.Contains(tree.prototypeIndex);
                    if (illusion && (terrainPosition + Vector3.Scale(tree.position, terrainSize)
                        - completionOrigin).sqrMagnitude <= radiusSquared) continue;
                    trees[remainingCount++] = tree;
                    if (illusion) hasIllusions = true;
                }
                if (remainingCount < trees.Count)
                {
                    trees.RemoveRange(remainingCount, trees.Count - remainingCount);
                    entry.data.SetTreeInstances(trees.ToArray(), false);
                }
                if (!hasIllusions) pendingTerrains.RemoveAt(terrainIndex);
            }
            bool pendingRenderers = false;
            foreach (var entry in hiddenRenderers)
            {
                if (entry.renderer == null) continue;
                // Combined terrain proxies span many trees: the shader masks each fragment
                // at wave arrival; disable the entire renderer only once its bounds are passed.
                Bounds bounds = entry.renderer.bounds;
                if (radius >= Vector3.Distance(bounds.center, completionOrigin) + bounds.extents.magnitude)
                    entry.renderer.enabled = false;
                else pendingRenderers = true;
            }
            if (pendingTerrains.Count == 0 && !pendingRenderers) completionStarted = -1f;
        }

        private void OnDisable()
        {
            due = started = -1f;
            Shader.SetGlobalFloat("_GrassWoodsEchoValid", 0f);
            Shader.SetGlobalFloat("_WoodsReplyActive", 0f);
            if (audioSource != null) audioSource.Stop();
        }

        private void OnDestroy()
        {
            if (falseWalls != null) falseWalls.SetActive(falseWallsWereActive);
            foreach (var entry in hiddenRenderers)
                if (entry.renderer != null) entry.renderer.enabled = entry.enabled;
            foreach (var entry in copies)
            {
                TerrainData restored = TerrainMuseVegetationProxy.ResolveRestoredData(entry.original);
                if (entry.terrain != null && entry.terrain.terrainData == entry.copy) entry.terrain.terrainData = restored;
                if (entry.collider != null && entry.collider.terrainData == entry.copy) entry.collider.terrainData = restored;
                Destroy(entry.copy);
            }
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat("_WoodsCompletionActive", 0f);
        }
    }
}
