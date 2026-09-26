using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Converts simulation grid coordinates into world positions for the map view. Grid row 0 is
    /// the top row, so Y is mirrored; the grid is centred on the world origin.
    /// </summary>
    /// <remarks>
    /// Named <c>MapLayout</c> rather than <c>GridLayout</c> on purpose: <c>UnityEngine.GridLayout</c>
    /// is a real engine type, and a same-named class in this namespace would silently shadow it.
    /// </remarks>
    public sealed class MapLayout
    {
        /// <summary>Grid width in cells (mirrors <see cref="NetworkGraph.Width"/>).</summary>
        public int Width { get; }

        /// <summary>Grid height in cells (mirrors <see cref="NetworkGraph.Height"/>).</summary>
        public int Height { get; }

        /// <summary>World size of one cell.</summary>
        public float CellSize { get; }

        /// <summary>Total world width covered by the grid, excluding margins.</summary>
        public float ContentWidth => Width * CellSize;

        /// <summary>Total world height covered by the grid, excluding margins.</summary>
        public float ContentHeight => Height * CellSize;

        public MapLayout(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
        }

        /// <summary>World-space centre of grid cell (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public Vector2 CellToWorld(int x, int y)
        {
            float wx = (x - (Width - 1) * 0.5f) * CellSize;
            float wy = ((Height - 1) * 0.5f - y) * CellSize;
            return new Vector2(wx, wy);
        }

        /// <summary>World-space centre of the cell occupied by <paramref name="node"/>.</summary>
        public Vector2 NodeToWorld(Node node) => CellToWorld(node.X, node.Y);

        /// <summary>
        /// Inverse of <see cref="CellToWorld"/>: the grid cell containing a world point.
        /// </summary>
        /// <param name="world">World-space point, typically <c>Camera.ScreenToWorldPoint(Input.mousePosition)</c>.</param>
        /// <param name="x">Grid column, only meaningful when this returns true.</param>
        /// <param name="y">Grid row, only meaningful when this returns true.</param>
        /// <returns>False when the point falls outside the grid.</returns>
        /// <remarks>
        /// <see cref="CellToWorld"/> returns a cell <i>centre</i>, so the half-cell offset below is
        /// load-bearing: without it the cell boundaries would land on the cell centres and every
        /// click in the upper-left quarter of a cell would resolve to its neighbour.
        /// </remarks>
        public bool TryWorldToCell(Vector2 world, out int x, out int y)
        {
            float column = world.x / CellSize + (Width - 1) * 0.5f;
            float row = (Height - 1) * 0.5f - world.y / CellSize;

            x = Mathf.FloorToInt(column + 0.5f);
            y = Mathf.FloorToInt(row + 0.5f);

            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// <summary>World X of the vertical grid line with index <paramref name="index"/> (0..<see cref="Width"/>).</summary>
        public float VerticalGridLineX(int index) => (index - Width * 0.5f) * CellSize;

        /// <summary>World Y of the horizontal grid line with index <paramref name="index"/> (0..<see cref="Height"/>).</summary>
        public float HorizontalGridLineY(int index) => (Height * 0.5f - index) * CellSize;
    }
}
