using UnityEngine;
using Unity.Cinemachine;
using Sandbox.Player;
using Rewired;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;


public struct CameraControlsComponent : IComponentData
{
    public LocalTransform localTransform;
    public float fov;
    public bool active;
    public float3 forward;
    public float3 right;
}

public class CameraControls : MonoBehaviour
{
    public Rewired.Player player;
    public int playerId = 0; // The Rewired player id of this character
    private bool changeX, changeY;
    [Header("Free Look Rotation")] public CinemachineCamera freeLook;

    public CinemachineFollow follow;
    public float minValueX = -360;
    public float maxValueX = 360;
    public float minHeight = 1;
    [Tooltip("Max height is relative to FOV")]
    public float maxHeight = 24f;
    public float minRadius = 1;
    public float maxRadius = 120;
    public float minFov = 6;
    public float maxFov = 120;
    public float xAxisValue;
    public float heightY;
    public float multiplierX = 30;
    public float multiplierY = 24;

    private float startHeight;
    private float startRadius;
    private Vector3 startRotationDamping;
    private float startFov;
    private float fovValue;
    float radiusValue;
    [SerializeField] bool aimMode;
    private float fovHeightAdj;


    [SerializeField] PlayerWeaponAim playerWeaponAimReference;
 
    void Start()
    {
        if (!ReInput.isReady) return;
        player = ReInput.players.GetPlayer(playerId);
        startHeight = follow.FollowOffset.y;
        xAxisValue = follow.FollowOffset.x;
        //startHeight = offset.Offset.y;
        //startRadius = offset.Offset.x;
        startRotationDamping = follow.TrackerSettings.RotationDamping;
        startFov = freeLook.Lens.FieldOfView;
        fovValue = startFov;
        radiusValue = startRadius;
        heightY = startHeight;
        ChangeFov(false);
        

    }

    void LateUpdate()
    {
        var controller = player.controllers.GetLastActiveController();
        var aimDisabled = false;
        if (playerWeaponAimReference)
        {
            aimMode = playerWeaponAimReference.aimMode;
            aimDisabled = playerWeaponAimReference.aimDisabled;
        }
        
        if (aimMode)
        {
            follow.TrackerSettings.RotationDamping = startRotationDamping * 10;
        }
        else
        {
            follow.TrackerSettings.RotationDamping = startRotationDamping * 1; 
        }

        

        if (controller == null || aimMode && !aimDisabled) return;//if aim Disabled completely then always allow right stick cam controls

        var gamePad = controller.type == ControllerType.Joystick;
        var keyboard = controller.type == ControllerType.Keyboard;
        bool modifier = player.GetButton("RightBumper"); // get the "held" state of the button

        changeX = true;
        changeY = true;

        if (player.GetAxis("RightVertical") >= .25)
        {
            if (!modifier)
            {
                heightY -= Time.deltaTime * multiplierY;
            }
            else
            {
                fovValue -= Time.deltaTime * multiplierY;
            }
            ChangeFov(modifier);
        }
        else if (player.GetAxis("RightVertical") <= -.25)
        {
            if (!modifier)
            {
                heightY += Time.deltaTime * multiplierY;
            }
            else
            {
                fovValue += Time.deltaTime * multiplierY;
            }

            ChangeFov(modifier);
        }

        if (player.GetAxis("RightHorizontal") <= -.25)
        {
            xAxisValue += Time.deltaTime * multiplierX;
            ChangeFov(modifier);
        }
        else if (player.GetAxis("RightHorizontal") >= .25)
        {
            xAxisValue -= Time.deltaTime * multiplierX;
            ChangeFov(modifier);
        }
    }


    public void ChangeFov(bool modifier)
    {
        if (freeLook)
        {
            
            
            if (changeX && !modifier)
            {
                xAxisValue = math.clamp(xAxisValue, minValueX, maxValueX);
                follow.FollowOffset.x = xAxisValue;
                //offset.Offset.x = xAxisValue;
            }

            if (changeY && !modifier)
            {
                var adjMaxHeight = startFov / fovValue * maxHeight;
                heightY = math.clamp(heightY, minHeight, adjMaxHeight);
                follow.FollowOffset.y = heightY;
                //offset.Offset.y = heightY;
            }
            else if (changeY)
            {
                fovValue = math.clamp(fovValue, minFov, maxFov);
                freeLook.Lens.FieldOfView = fovValue;
                //freeLook.m_Orbits[1].m_Radius = radiusValue;
            }
            
        }
    }
}