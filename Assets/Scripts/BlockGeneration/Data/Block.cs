using System.Collections.Generic;

#region reference
///https://github.com/Szuszi/CityGenerator-Unity/blob/main/CityGenerator2D/Assets/Procedural%20City%20Generator/Scripts/BlockGeneration/Block.cs
#endregion
namespace BlockGeneration
{
    public class Block
    {
        public List<BlockNode> Nodes { get; private set; }
        public float Height { get; set; }
        public bool IsOccupied { get; set; }

        public Block()
        {
            Nodes = new List<BlockNode>();
            Height = 0;
            IsOccupied = true;
        }
    }
}