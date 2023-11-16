using GraphModel;
using UnityEngine;

#region reference
/// https://github.com/Szuszi/CityGenerator-Unity/blob/main/CityGenerator2D/Assets/Procedural%20City%20Generator/Scripts/Services/VectorService.cs
#endregion
namespace Services
{
    static class VectorService
    {
        public static Vector2 NodesToDirection(Node NodeA, Node NodeB)
        {
            return new Vector2(NodeB.X - NodeA.X, NodeB.Y - NodeA.Y);
        }

        public static Vector2 NodesToNormal(Node NodeA, Node NodeB)
        {
            return new Vector2(NodeA.Y - NodeB.Y, -(NodeA.X - NodeB.X));
        }

        public static Vector2 DirectionToNormal(Vector2 dirVector)
        {
            return new Vector2(dirVector.y, -dirVector.x);
        }
    }
}
