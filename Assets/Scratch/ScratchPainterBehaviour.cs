using ScratchCardAsset;
using ScratchCardAsset.Core;
using UnityEngine;

namespace LoopGamesCaseStudy.AppleGrappleClone
{
    /// <summary>
    /// Bridges the game to the ScratchCard plugin: world position in, carved hole out.
    ///
    /// THIS FILE DELIBERATELY LIVES OUTSIDE Assets/Scripts. The plugin sits under
    /// Assets/Plugins, so it compiles into Assembly-CSharp-firstpass, and an asmdef
    /// assembly can never reference a predefined one. Assembly-CSharp can reference
    /// both, so the adapter belongs here — the game side only sees IScratchPainter.
    /// </summary>
    public sealed class ScratchPainterBehaviour : MonoBehaviour, IScratchPainter
    {
        [SerializeField] private ScratchCardManager _card;

        private bool _armed;

        /// <summary>
        /// The plugin initializes in Start(), GameManager composes in Awake() — so for
        /// the first frames ScratchData and the internal renderer are still null and any
        /// call would throw. Everything is gated on this.
        /// </summary>
        private bool IsReady => _card != null && _card.Card != null && _card.Card.Initialized;

        public void Paint(Vector2 worldPosition, float brushSize)
        {
            if (!IsReady) return;
            if (!_armed) Arm();

            Camera camera = _card.MainCamera;
            if (camera == null) return;

            // Set per call: characters and swords carve at different widths, and the
            // plugin exposes only one global brush size.
            _card.Card.BrushSize = brushSize;

            // Camera-relative on purpose. Screen shake moves the camera, but the same
            // camera is used to project and to raycast the surface, so the mark still
            // lands on the right patch of ground.
            Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
            _card.Card.ScratchHole(_card.Card.ScratchData.GetScratchPosition(screenPoint));
        }

        public void ClearAll()
        {
            if (!IsReady) return;
            Wipe();
        }

        /// <summary>One-time setup, on the first frame the plugin is actually usable.</summary>
        private void Arm()
        {
            _armed = true;

            // Otherwise dragging the mouse carves the ground too — the trail is the
            // only thing allowed to write to this card.
            _card.InputEnabled = false;
            Wipe();
        }

        private void Wipe()
        {
            // Straight to the card, NOT through ScratchCardManager.Clear/FillScratchCard:
            // those also poke EraseProgress, whose command buffer is still null this early
            // and logs an error. We never read the progress percentage anyway.
            //
            // Erase mode: clear is 0% scratched. Restore inverts it — same split as
            // ScratchCardManager.Init, so a reset matches a fresh start.
            // false = leave IsScratched alone; a wipe is not a scratch.
            if (_card.Card.Mode == ScratchMode.Restore) _card.Card.Fill(false);
            else                                        _card.Card.Clear(false);
        }

        private void Reset() => _card = GetComponent<ScratchCardManager>();
    }
}
