﻿//
// Copyright (C) Microsoft. All rights reserved.
// TODO This needs to be validated for HoloToolkit integration
//

using System;
using UnityEngine;

namespace HoloToolkit.Unity.InputModule
{
    /// <summary>
    /// Object that represents a cursor in 3D space controlled by gaze.
    /// </summary>
    public abstract class Cursor : MonoBehaviour, ICursor
    {
        /// <summary>
        /// Enum for current cursor state
        /// </summary>
        public enum CursorStateEnum
        {
            /// <summary>
            /// Useful for releasing external override.
            /// See <c>CursorStateEnum.Contextual</c>
            /// </summary>
            None = -1,
            /// <summary>
            /// Not IsHandVisible
            /// </summary>
            Observe,
            /// <summary>
            /// IsHandVisible AND not IsInputSourceDown AND TargetedObject is NULL
            /// </summary>
            Interact,
            /// <summary>
            /// IsHandVisible AND not IsInputSourceDown AND TargetedObject exists
            /// </summary>
            Hover,
            /// <summary>
            /// IsHandVisible AND IsInputSourceDown
            /// </summary>
            Select,
            /// <summary>
            /// Available for use by classes that extend Cursor.
            /// No logic for setting Release state exists in the base Cursor class.
            /// </summary>
            Release,
            /// <summary>
            /// Allows for external override
            /// </summary>
            Contextual
        }


        public CursorStateEnum CursorState { get { return cursorState; } }
        private CursorStateEnum cursorState = CursorStateEnum.None;

        /// <summary>
        /// Minimum distance for cursor if nothing is hit
        /// </summary>
        [Header("Cusor Distance")]
        [Tooltip("The minimum distance the cursor can be with nothing hit")]
        public float MinCursorDistance = 1.0f;

        /// <summary>
        /// Maximum distance for cursor if nothing is hit
        /// </summary>
        [Tooltip("The maximum distance the cursor can be with nothing hit")]
        public float DefaultCursorDistance = 2.0f;

        /// <summary>
        /// Surface distance to place the cursor off of the surface at
        /// </summary>
        [Tooltip("The distance from the hit surface to place the cursor")]
        public float SurfaceCursorDistance = 0.02f;

        [Header("Motion")]
        /// <summary>
        /// Blend value for surface normal to user facing lerp
        /// </summary>
        public float PositionLerpTime = 0.01f;

        /// <summary>
        /// Blend value for surface normal to user facing lerp
        /// </summary>
        public float ScaleLerpTime = 0.01f;

        /// <summary>
        /// Blend value for surface normal to user facing lerp
        /// </summary>
        public float RotationLerpTime = 0.01f;

        /// <summary>
        /// Blend value for surface normal to user facing lerp
        /// </summary>
        [Range(0, 1)]
        public float LookRotationBlend = 0.5f;

        [Header("Tranform References")]
        /// <summary>
        /// Visual that is displayed when cursor is active normally
        /// </summary>
        public Transform PrimaryCursorVisual;

        /// <summary>
        /// Get position accessor for modifiers;
        /// </summary>
        public Vector3 GetPosition()
        {
            return transform.position;
        }
        
        /// <summary>
        /// Get rotation accessor for modifiers;
        /// </summary>
        public Quaternion GetRotation()
        {
            return transform.rotation;
        }

        /// <summary>
        /// Get scale accessor for modifiers;
        /// </summary>
        public Vector3 GetScale()
        {
            return transform.localScale;
        }

        /// <summary>
        /// Indicates if hand is current in the view
        /// </summary>
        protected bool IsHandVisible;

        /// <summary>
        /// Indicates air tap down
        /// </summary>
        protected bool IsInputSourceDown;

        protected GameObject TargetedObject;
        protected ICursorModifier TargetedCursorModifier;

        private bool isRegisteredToGazeManager = false;
        private bool isInputRegistered = false;

        private uint visibleHandsCount = 0;
        private bool isVisible = true;

        private GazeManager gazeManager;

        /// <summary>
        /// Position, scale and rotational goals for cursor
        /// </summary>
        private Vector3 targetPosition;
        private Vector3 targetScale;
        private Quaternion targetRotation;

        /// <summary>
        /// Indicates if the cursor should be visible
        /// </summary>
        public bool IsVisible
        {
            set
            {
                isVisible = value;
                SetVisiblity(isVisible);
            }
        }

#region MonoBehaviour Functions

        private void Awake()
        {
            // Use the setter to update visibility of the cursor at startup based on user preferences
            IsVisible = isVisible;
            SetVisiblity(isVisible);
        }

        private void Start()
        {
            gazeManager = GazeManager.Instance;

            RegisterGazeManager();
            RegisterInput();
        }

        private void Update()
        {
            UpdateCursorState();
            UpdateCursorTransform();
        }

        /// <summary>
        /// Override for enable functions
        /// </summary>
        protected virtual void OnEnable(){}

        /// <summary>
        /// Override for disable functions
        /// </summary>
        protected virtual void OnDisable()
        {
            TargetedObject = null;
            TargetedCursorModifier = null;
            visibleHandsCount = 0;
        }

        private void OnDestroy()
        {
            UnregisterInput();
            UnregisterGazeManager();
        }

#endregion

        /// <summary>
        /// Register to events from the gaze manager, if not already registered.
        /// </summary>
        private void RegisterGazeManager()
        {
            if (!isRegisteredToGazeManager && gazeManager != null)
            {
                gazeManager.FocusedObjectChanged += OnFocusedObjectChanged;
                isRegisteredToGazeManager = true;
            }
        }

        /// <summary>
        /// Unregister from events from the gaze manager.
        /// </summary>
        private void UnregisterGazeManager()
        {
            if (isRegisteredToGazeManager && gazeManager != null)
            {
                gazeManager.FocusedObjectChanged -= OnFocusedObjectChanged;
                isRegisteredToGazeManager = false;
            }
        }

        /// <summary>
        /// Register to input events that can impact cursor state.
        /// </summary>
        private void RegisterInput()
        {
            if (isInputRegistered || InputManager.Instance == null)
            {
                return;
            }

            // Register the cursor as a global listener, so that it can always get input events it cares about
            InputManager.Instance.AddGlobalListener(gameObject);
            isInputRegistered = true;
        }

        /// <summary>
        /// Unregister from input events.
        /// </summary>
        private void UnregisterInput()
        {
            if (!isInputRegistered)
            {
                return;
            }

            if (InputManager.Instance != null)
            {
                InputManager.Instance.RemoveGlobalListener(gameObject);
                isInputRegistered = false;
            }
        }

        /// <summary>
        /// Updates the currently targeted object and cursor modifier upon getting
        /// an event indicating that the focused object has changed.
        /// </summary>
        /// <param name="previousObject">Object that was previously being focused.</param>
        /// <param name="newObject">New object being focused.</param>
        protected virtual void OnFocusedObjectChanged(GameObject previousObject, GameObject newObject)
        {
            TargetedObject = newObject;
            if (newObject != null)
            {
                OnActiveModifier(newObject.GetComponent<CursorModifier>());
            }
        }

        /// <summary>
        /// Override function when a new modifier is found or no modifier is valid
        /// </summary>
        /// <param name="modifier"></param>
        protected virtual void OnActiveModifier(CursorModifier modifier)
        {
            if (modifier != null)
            {
                modifier.RegisterCursor(this);
            }

            TargetedCursorModifier = modifier;
        }

        /// <summary>
        /// Update the cursor's transform
        /// </summary>
        private void UpdateCursorTransform()
        {
            // Get the necessary info from the gaze source
            RaycastHit hitResult = gazeManager.HitInfo;
            GameObject newTargetedObject = gazeManager.HitObject;

            // Get the forward vector looking back at camera
            Vector3 lookForward = -gazeManager.GazeNormal;

            // Normalize scale on before update
            targetScale = Vector3.one;

            // If no game object is hit, put the cursor at the default distance
            if (TargetedObject == null)
            {
                this.TargetedObject = null;
                targetPosition = gazeManager.GazeOrigin + gazeManager.GazeNormal * DefaultCursorDistance;
                targetRotation = lookForward.magnitude > 0 ? Quaternion.LookRotation(lookForward, Vector3.up) : transform.rotation;
            }
            else
            {
                // Update currently targeted object
                this.TargetedObject = newTargetedObject;

                if (TargetedCursorModifier != null)
                {
                    TargetedCursorModifier.GetModifierTranslation(out targetPosition, out targetRotation, out targetScale);
                }
                else
                {
                    // If no modifier is on the target, just use the hit result to set cursor position
                    targetPosition = hitResult.point + (lookForward * SurfaceCursorDistance);
                    targetRotation = Quaternion.LookRotation(Vector3.Lerp(hitResult.normal, lookForward, LookRotationBlend), Vector3.up);
                }
            }

            // Use the lerp times to blend the position to the target position
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime / PositionLerpTime);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime / ScaleLerpTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime / RotationLerpTime);
        }

        /// <summary>
        /// Updates the visual representation of the cursor.
        /// </summary>
        public void SetVisiblity(bool visible)
        { 
            if (PrimaryCursorVisual != null)
            {
                PrimaryCursorVisual.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Disable input and set to contextual to override input
        /// </summary>
        public virtual void DisableInput()
        {
            // Reset visible hands on disable
            visibleHandsCount = 0;
            IsHandVisible = false;

            OnCursorStateChange(CursorStateEnum.Contextual);
        }

        /// <summary>
        /// Enable input and set to none to reset cursor
        /// </summary>
        public virtual void EnableInput()
        {
            OnCursorStateChange(CursorStateEnum.None);
        }

        /// <summary>
        /// Function for consuming the OnInputUp events
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnInputUp(InputEventData eventData)
        {
            if (IsInputSourceDown == false)
            {
                return;
            }
            IsInputSourceDown = false;
        }

        /// <summary>
        /// Function for receiving OnInputDown events from InputManager
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnInputDown(InputEventData eventData)
        {
            IsInputSourceDown = true;
        }

        /// <summary>
        /// Function for receiving OnInputClicked events from InputManager
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnInputClicked(InputEventData eventData)
        {
            // Open input socket for other cool stuff...
        }


        /// <summary>
        /// Input source detected callback for the cursor
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnSourceDetected(SourceStateEventData eventData)
        {
            visibleHandsCount++;
            IsHandVisible = true;
        }


        /// <summary>
        /// Input source lost callback for the cursor
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void OnSourceLost(SourceStateEventData eventData)
        {
            visibleHandsCount--;
            if (visibleHandsCount == 0)
            {
                IsHandVisible = false;
            }
        }

        /// <summary>
        /// Internal update to check for cursor state changes
        /// </summary>
        private void UpdateCursorState()
        {
            CursorStateEnum newState = CheckCursorState();
            if (cursorState != newState)
            {
                OnCursorStateChange(newState);
            }
        }

        /// <summary>
        /// Virtual function for checking state changess.
        /// </summary>
        public virtual CursorStateEnum CheckCursorState()
        {
            if (cursorState != CursorStateEnum.Contextual)
            {
                if (IsInputSourceDown)
                {
                    return CursorStateEnum.Select;
                }
                else if(cursorState == CursorStateEnum.Select)
                {
                    return CursorStateEnum.Release;
                }

                if (IsHandVisible)
                {
                    if (TargetedObject != null)
                    {
                        return CursorStateEnum.Hover;
                    }
                    return CursorStateEnum.Interact;
                }
                return CursorStateEnum.Observe;
            }
            return CursorStateEnum.Contextual;
        }

        /// <summary>
        /// Change the cursor state to the new state.  Override in cursor implementations.
        /// </summary>
        /// <param name="state"></param>
        public virtual void OnCursorStateChange(CursorStateEnum state)
        {
            cursorState = state;
        }
    }
}