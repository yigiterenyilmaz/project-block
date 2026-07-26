// PURPOSE: A cube that does not stay on the board - it appears where it was placed, then falls
// straight down under gravity, out of the arena and off the bottom of the screen. Spawn-and-forget,
// exactly like FloatingTextFx.
//
// This is what a DEFECTIVE SMUGGLED block looks like ("Kaçakçı"): the play is legal, the cubes show
// up for a moment, and then they are gone and so is the turn. Purely cosmetic - the rules already
// decided nothing landed (RoundEngine never places the card), and this only shows the player why.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>A single cube falling off the screen. Destroys itself when it is well clear.</summary>
    public sealed class FallingCubeFx : MonoBehaviour
    {
        /// <summary>Starting downward speed, in world units per second. Small, so the cube reads
        /// as "let go" rather than "fired".</summary>
        private const float InitialSpeed = 0.6f;

        /// <summary>Downward acceleration. High enough that the fall is over quickly.</summary>
        private const float Gravity = 26f;

        /// <summary>How long the cube hangs in place before it lets go, in seconds. Staggered per
        /// cube by the spawner so a block crumbles rather than dropping as one slab.</summary>
        private float holdFor;

        /// <summary>Below this local Y the cube is off-screen for good.</summary>
        private const float KillY = -14f;

        private const float SpinDegreesPerSecond = 140f;

        private SpriteRenderer sprite;
        private Color baseColor;
        private float velocity;
        private float age;
        private float spinDirection;

        public static void Spawn(Transform parent, Vector2 position, float size, Color color,
            float delay)
        {
            SpriteRenderer renderer = ViewUtil.MakeCell(parent, "FallingCube", position, size,
                color, 40);
            FallingCubeFx fx = renderer.gameObject.AddComponent<FallingCubeFx>();
            fx.sprite = renderer;
            fx.baseColor = color;
            fx.holdFor = delay;
            fx.velocity = InitialSpeed;
            // Deterministic-looking tumble without needing a seed: the spawn position decides
            // which way it turns, so the same block always crumbles the same way.
            fx.spinDirection = ((int)(position.x * 8f) & 1) == 0 ? 1f : -1f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < holdFor)
            {
                return;
            }
            float step = Time.deltaTime;
            velocity += Gravity * step;
            transform.localPosition += new Vector3(0f, -velocity * step, 0f);
            transform.Rotate(0f, 0f, spinDirection * SpinDegreesPerSecond * step);
            // Fades over the last stretch of the drop, so it thins out instead of popping.
            Color color = baseColor;
            color.a = Mathf.Clamp01(1f - Mathf.Max(0f, velocity - 6f) / 14f);
            sprite.color = color;
            if (transform.localPosition.y < KillY)
            {
                Destroy(gameObject);
            }
        }
    }
}
