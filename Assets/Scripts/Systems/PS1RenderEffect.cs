using UnityEngine;

namespace SoulsLike.Systems
{
    /// <summary>
    /// Attach to the main camera. Renders the scene to a small, point-filtered
    /// RenderTexture and blits it back up - the classic "chunky pixel" PS1 look.
    /// Requires Shaders/PS1Dither.shader to be present in the project.
    /// Built-in Render Pipeline only (uses OnRenderImage).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PS1RenderEffect : MonoBehaviour
    {
        [Header("Internal Resolution")]
        [Tooltip("Height of the internal render target in pixels. PS1 games ran around 240-256p.")]
        public int internalHeight = 240;

        [Header("Dithering")]
        public bool ditherEnabled = true;
        [Range(0f, 1f)] public float ditherStrength = 0.06f;
        [Tooltip("Assign Shaders/PS1Dither.shader here, or leave empty to try Shader.Find at runtime.")]
        public Shader ditherShader;

        private Material ditherMat;
        private static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");
        private RenderTexture lowResTarget;

        private void Awake()
        {
            Shader shader = ditherShader != null ? ditherShader : Shader.Find("Hidden/SoulsLike/PS1Dither");
            if (shader != null)
                ditherMat = new Material(shader);
            else
                Debug.LogWarning("PS1RenderEffect: dither shader not found. Assign it in the Inspector, or the effect will run without dithering.");
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            int height = Mathf.Max(64, internalHeight);
            int width = Mathf.RoundToInt(height * ((float)source.width / source.height));

            if (lowResTarget == null || lowResTarget.width != width || lowResTarget.height != height)
            {
                if (lowResTarget != null) lowResTarget.Release();
                lowResTarget = new RenderTexture(width, height, 0)
                {
                    filterMode = FilterMode.Point,
                    antiAliasing = 1
                };
            }

            Graphics.Blit(source, lowResTarget);

            if (ditherEnabled && ditherMat != null)
            {
                ditherMat.SetFloat(DitherStrengthId, ditherStrength);
                Graphics.Blit(lowResTarget, destination, ditherMat);
            }
            else
            {
                Graphics.Blit(lowResTarget, destination);
            }
        }

        private void OnDestroy()
        {
            if (lowResTarget != null) lowResTarget.Release();
        }
    }
}
