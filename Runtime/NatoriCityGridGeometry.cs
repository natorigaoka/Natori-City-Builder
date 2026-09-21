using UnityEngine;

namespace Natori.CityBuilder
{
    public static class NatoriCityGridGeometry
    {
        public static Vector2 GetGridMinimum(Vector2Int gridSize, float horizontalCellSize)
        {
            return new Vector2(
                -gridSize.x * horizontalCellSize * 0.5f,
                -gridSize.y * horizontalCellSize * 0.5f);
        }

        public static Vector2Int GetRotatedFootprint(Vector2Int footprint, int quarterTurnsClockwise)
        {
            int normalizedQuarterTurns = ((quarterTurnsClockwise % 4) + 4) % 4;
            if ((normalizedQuarterTurns & 1) == 0)
            {
                return footprint;
            }
            return new Vector2Int(footprint.y, footprint.x);
        }

        public static bool IsFootprintInsideGrid(
            Vector2Int anchorCell,
            Vector2Int rotatedFootprint,
            Vector2Int gridSize)
        {
            return anchorCell.x >= 0
                && anchorCell.y >= 0
                && anchorCell.x + rotatedFootprint.x <= gridSize.x
                && anchorCell.y + rotatedFootprint.y <= gridSize.y;
        }

        public static Vector3 GetPlacementLocalPosition(
            Vector2Int gridSize,
            Vector2Int anchorCell,
            Vector2Int rotatedFootprint,
            float floorBaseHeight,
            float horizontalCellSize)
        {
            Vector2 gridMinimum = GetGridMinimum(gridSize, horizontalCellSize);
            return new Vector3(
                gridMinimum.x + (anchorCell.x + rotatedFootprint.x * 0.5f) * horizontalCellSize,
                floorBaseHeight,
                gridMinimum.y + (anchorCell.y + rotatedFootprint.y * 0.5f) * horizontalCellSize);
        }

        public static float GetFloorBaseHeight(NatoriCityBuildingComponent building, int floorIndex)
        {
            float floorBaseHeight = 0.0f;
            for (int floorSeek = 0; floorSeek < floorIndex; floorSeek++)
            {
                floorBaseHeight += building.Floors[floorSeek].BuildingGroup.Height;
            }
            return floorBaseHeight;
        }

        public static bool DoesPlacementOccupyCell(BuildingPartPlacement placement, Vector2Int cell)
        {
            Vector2Int rotatedFootprint = GetRotatedFootprint(
                placement.FootprintAtPlacement,
                placement.QuarterTurnsClockwise);
            return cell.x >= placement.AnchorCell.x
                && cell.y >= placement.AnchorCell.y
                && cell.x < placement.AnchorCell.x + rotatedFootprint.x
                && cell.y < placement.AnchorCell.y + rotatedFootprint.y;
        }
    }
}
