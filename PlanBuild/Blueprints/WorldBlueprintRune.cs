﻿using UnityEngine;

namespace PlanBuild.Blueprints
{
    internal class WorldBlueprintRune : MonoBehaviour, Interactable, Hoverable
    {
        private Piece m_piece;

        public static bool JustDeactivated { get; set; } = false;

        public void Awake()
        {
            m_piece = GetComponent<Piece>();
        }

        public void LateUpdate()
        {
            JustDeactivated = false;
        }

        public string GetHoverName()
        {
            return m_piece.m_name;
        }

        public string GetHoverText()
        {
            return Localization.instance.Localize(
                $"{GetHoverName()}\n" +
                $"[<color=yellow>$KEY_Use</color>] Open Blueprint Marketplace"
            );
        }

        public bool Interact(Humanoid user, bool hold)
        {
            if (JustDeactivated || hold)
            {
                return false;
            }
            BlueprintGUI.Instance.Toggle();
            return false;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item)
        {
            return false;
        }
    }
}