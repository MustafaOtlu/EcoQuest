using UnityEngine;

public class GameConstants : MonoBehaviour
{
    public const float DAY_DURATION_SECONDS = 300f;
    public const float DAWN_START = 0.2f;
    public const float DAY_START = 0.3f;
    public const float DUSK_START = 0.7f;
    public const float NIGHT_START = 0.8f;

    public const int SOLAR_PANEL_METAL_COST = 256;
    public const int SOLAR_PANEL_PLASTIC_COST = 128;
    public const float SOLAR_PANEL_KWH_PER_DAY = 2f;

    public const int WIND_TURBINE_METAL_COST = 2560;
    public const int WIND_TURBINE_PLASTIC_COST = 1280;
    public const float WIND_TURBINE_KWH_PER_DAY = 120f;

    public const int RECYCLING_SMALL_METAL = 128;
    public const int RECYCLING_SMALL_PLASTIC = 64;
    public const int RECYCLING_MEDIUM_METAL = 256;
    public const int RECYCLING_MEDIUM_PLASTIC = 128;
    public const int RECYCLING_LARGE_METAL = 512;
    public const int RECYCLING_LARGE_PLASTIC = 256;

    public const int WATER_TREATMENT_SMALL_COST = 64;
    public const int WATER_TREATMENT_MEDIUM_COST = 128;
    public const int WATER_TREATMENT_LARGE_COST = 256;

    public const int CABLE_METAL_PER_TILE = 1;
    public const int CABLE_PLASTIC_PER_TILE = 1;
    public const int PIPE_PLASTIC_PER_TILE = 1;

    public const int MAX_YEP_LEVEL = 48;
    public const float YEP_PER_LEVEL = 100f;

    public const int MAX_WATER_TREATMENT = 8;
    public const int MAX_WATER_STORAGE = 8;

    public const float PLAYER_MOVE_SPEED = 5f;
    public const float CAMERA_FOLLOW_SPEED = 8f;
    public const float BUILD_MODE_CAMERA_SPEED = 10f;
    public const float BUILD_MODE_CAMERA_MAX_DISTANCE = 15f;
}
