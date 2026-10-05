using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public class NodeMetadataBasic
    {
        public Vector2 Position;
        // HLUnityCore.Runtime.dll:Hardlight.NodeMetadataBasic:0x06000c58;
        // arm64 0x1b0ae10 stores the supplied position.
        public NodeMetadataBasic(Vector2 position) { Position = position; }
    }

    [Serializable]
    public class NodeMetadata : NodeMetadataBasic
    {
        public string Guid;
        public int InDirection;
        public int OutDirection;
        public bool Flipped;
        // Original token 0x06000c56; arm64 0x1b0ad9c.
        public NodeMetadata(Vector2 position, System.Guid guid, int inDirection, int outDirection) : base(position)
        {
            Guid = guid.ToString();
            InDirection = inDirection;
            OutDirection = outDirection;
        }

        // Original token 0x06000c57; arm64 0x1b0ae48. Valid strings are
        // preserved; invalid identifiers are replaced once with a new GUID.
        public System.Guid GetGuid()
        {
            if (System.Guid.TryParse(Guid, out System.Guid guid)) return guid;
            guid = System.Guid.NewGuid();
            Guid = guid.ToString();
            return guid;
        }
    }

    [Serializable]
    public class ViewMetadata
    {
        public Vector2 Translation;
        public float Zoom;
        // Original token 0x06000c59; arm64 0x1b0aed0.
        public ViewMetadata(Vector2 translation = default, float zoom = 1f)
        {
            Translation = translation;
            Zoom = zoom;
        }
    }

    [Serializable]
    public class BackgroundMetadata : NodeMetadata
    {
        public string Title;
        public Vector2 Size;
        public Color Colour;
        public bool MoveOverlaidNodes;
        // Original token 0x06000c55; arm64 0x1b0acd8 uses zero directions.
        public BackgroundMetadata(Vector2 position, System.Guid guid, string title, Vector2 size, Color colour, bool moveOverlaidNodes)
            : base(position, guid, 0, 0)
        {
            Title = title;
            Size = size;
            Colour = colour;
            MoveOverlaidNodes = moveOverlaidNodes;
        }
    }
}
