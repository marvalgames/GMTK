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
    [Header("Free Look Rotation")] public CinemachineVirtualCameraBase freeLook;

    public CinemachineFollow follow;
    //public CinemachineFreeLook freeLookCombat;
    //public  DefaultInputAxisDriver xAxis;
    //public DefaultInputAxisDriver   yAxis;
    //[SerializeField] private CinemachineCameraOffset offset;
    public float minValueX = -360;
    public float maxValueX = 360;
    public float minHeight = 1;
    public float maxHeight = 24f;
    public float minRadius = 1;
    public float maxRadius = 120;
    public float xAxisValue;
    public float heightY;
    public float multiplierX = 30;
    public float multiplierY = 24;

    private float startHeight;
    private float startRadius;
    float radiusValue;


    [SerializeField] PlayerWeaponAim playerWeaponAimReference;
 
    // private void OnValidate()
    // {
    //     xAxis.Validate();
    //     yAxis.Validate();
    // }
    //
    // private void Reset()
    // {
    //     xAxis = new DefaultInputAxisDriver
    //     {
    //         
    //         multiplier = -10f,
    //         accelTime = 0.1f,
    //         decelTime = 0.1f,
    //         name = "Mouse X",
    //     };
    //     yAxis = new DefaultInputAxisDriver
    //     {
    //         multiplier = 0.1f,
    //         accelTime = 0.1f,
    //         decelTime = 0.1f,
    //         name = "Mouse Y",
    //     };
    // }


    void Start()
    {
        if (!ReInput.isReady) return;
        player = ReInput.players.GetPlayer(playerId);
        startHeight = follow.FollowOffset.y;
        xAxisValue = follow.FollowOffset.x;
        //startHeight = offset.Offset.y;
        //startRadius = offset.Offset.x;
        radiusValue = startRadius;
        heightY = startHeight;
        ChangeFov(false);
        //offset = new CinemachineCameraOffset();
        
        
    }

    void LateUpdate()
    {
        var controller = player.controllers.GetLastActiveController();
        var aimMode = false;
        var aimDisabled = false;
        if (playerWeaponAimReference)
        {
            aimMode = playerWeaponAimReference.aimMode;
            aimDisabled = playerWeaponAimReference.aimDisabled;
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
                radiusValue -= Time.deltaTime * multiplierY;
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
                radiusValue += Time.deltaTime * multiplierY;
            }

            ChangeFov(modifier);
        }

        if (player.GetAxis("RightHorizontal") >= .25)
        {
            xAxisValue += Time.deltaTime * multiplierX;
            ChangeFov(modifier);
        }
        else if (player.GetAxis("RightHorizontal") <= -.25)
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
                heightY = math.clamp(heightY, minHeight, maxHeight);
                follow.FollowOffset.y = heightY;
                //offset.Offset.y = heightY;
            }
            else if (changeY)
            {
                radiusValue = math.clamp(radiusValue, minRadius, maxRadius);
                //freeLook.m_Orbits[1].m_Radius = radiusValue;
            }
            
        }
    }
}