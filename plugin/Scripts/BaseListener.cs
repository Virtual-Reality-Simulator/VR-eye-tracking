﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PupilLabs
{
    public abstract class BaseListener
    {
        public bool IsListening { get; private set; }

        protected SubscriptionsController subsCtrl;

        public BaseListener(SubscriptionsController subsCtrl)
        {
            this.subsCtrl = subsCtrl;
        }

        ~BaseListener()
        {
            if (subsCtrl.IsConnected)
            {
                Disable();
            }
        }

        public void Enable()
        {
            if (!subsCtrl.IsConnected)
            {
                Debug.LogWarning("No connected!");
                return;
            }

            if (IsListening)
            {
                Debug.Log("Already listening.");
                return;
            }

            CustomEnable();

            IsListening = true;
        }

        protected abstract void CustomEnable();

        public void Disable()
        {
            if (!subsCtrl.IsConnected)
            {
                Debug.Log("Not connected.");
                return;
            }

            if (!IsListening)
            {
                Debug.Log("Not running.");
                return;
            }

            CustomDisable();
            IsListening = false;
        }

        protected abstract void CustomDisable();
    }
}