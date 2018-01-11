using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using vJoyInterfaceWrap;

namespace FeederDemoCS
{
    public partial class FeederForm : Form
    {
        static public vJoy joystick;
        static public uint id = 1;
        bool res;
        int X, Y, Z, ZR, XR;
        double cThr, cPitch, cRoll, cYaw, cConv;
        double count = 0;
        double random_gen = 0;
        ERAState state;
        long maxval = 0;
        bool allownoise = false;
        bool copter = true;
        int DiscPovNumber;
        int ContPovNumber;

        enum ERAState { Disarmed, Arming, ThrottleUp, Conversion, Idle, PitchUp, PitchDown, 
            RollDown, RollUp, YawLeft,YawRight }; 
        public FeederForm()
        {
            InitializeComponent();
        }

        public void setFeeder(vJoy j,uint i)
        {
            id = i;
            joystick = j;
        }

        private void FeederForm_Load(object sender, EventArgs e)
        {
            DiscPovNumber = joystick.GetVJDDiscPovNumber(id);
            ContPovNumber = joystick.GetVJDContPovNumber(id);
            joystick.GetVJDAxisMax(id, HID_USAGES.HID_USAGE_X, ref maxval);
            // Reset this device to default values
            joystick.ResetVJD(id);
            X = 20;
            Y = 30;
            Z = 40;
            XR = 60;
            ZR = 80;
            cPitch = cRoll = cYaw = 0.5;
            cThr = 0;
            cConv = 0;
            state = ERAState.Disarmed;
            label2.Text = "НЕ ВЗВЕДЕНО";
        }

        private void setAxes()
        {
            // Set position of 4 axes
            X = (int)((double)maxval * cPitch);
            Y = (int)((double)maxval * cRoll);
            Z = (int)((double)maxval * cThr);
            XR = (int)((double)maxval * cYaw);
            ZR = (int)((double)maxval * cConv);
            res = joystick.SetAxis(X, id, HID_USAGES.HID_USAGE_X);
            res = joystick.SetAxis(Y, id, HID_USAGES.HID_USAGE_Y);
            res = joystick.SetAxis(Z, id, HID_USAGES.HID_USAGE_Z);
            res = joystick.SetAxis(XR, id, HID_USAGES.HID_USAGE_RX);
            res = joystick.SetAxis(ZR, id, HID_USAGES.HID_USAGE_RZ);
        }
        private void Arm()
        {
            cPitch = cRoll = 0.5;
            cThr = 0;
            cYaw = 0;
            count = 3.0f;
            state = ERAState.Arming;
        }
        private void ArmReady()
        {
            cYaw = 0.5;
            state = ERAState.ThrottleUp;
            allownoise = true;
        }
        private void StartupThrottle()
        {
            cThr = cThr + (0.5 - cThr) / 2.0 * ((double)timer1.Interval) / 1000.0;
            if (Math.Abs(cThr-0.5)<0.01)
            {
                count = 0;
                cThr = 0.5;
                state = ERAState.Idle;
            }
        }
        private void WhatsNext()
        {
            count += ((double)timer1.Interval) / 1000.0;
            cPitch -= (cPitch - 0.5) * 0.05;
            cRoll -= (cRoll - 0.5) * 0.05;
            cThr -= (cThr - 0.5) * 0.05;
            cYaw -= (cYaw - 0.5) * 0.05;
            if (count > 1.0f)
            {
                count = 0;
                Random r = new Random();
                int rnd_num = (int)Math.Floor(r.NextDouble()*10);
                switch(rnd_num)
                {
                    case 0:
                    case 8:
                        state = ERAState.Conversion;
                        break;
                    case 9:
                        state = ERAState.Idle;
                        break;
                    default:
                        state = ERAState.Idle + rnd_num-1;
                        break;
                }
            }
        }

        private double generateStickWaveform(double time)
        {
            if (time < 0.2)
            {
                return 0;
            }
            if ((time >= 0.2)&&(time<1.5))
            {
                return (time-0.2)/(1.5-0.2);
            }
            if ((time >= 1.5) && (time < 2.8))
            {
                return (2.8-time) / (2.8 - 1.5);
            }
            if (time >= 2.8)
            {
                return 0;
            }
            return 0;
        }
        private void processStickMovement()
        {
            count += ((double)timer1.Interval) / 1000.0;
            double wf = generateStickWaveform(count*2);

            cPitch -= (cPitch - 0.5) * 0.05;
            cRoll -= (cRoll - 0.5) * 0.05;
            cThr -= (cThr - 0.5) * 0.05;
            cYaw -= (cYaw - 0.5) * 0.05;

            switch (state)
            {
                case ERAState.PitchUp:
                    label2.Text = "ТАНГАЖ+";
                    cPitch = 0.5 + wf * 0.25;
                    break;
                case ERAState.PitchDown:
                    label2.Text = "ТАНГАЖ-";
                    cPitch = 0.5 - wf * 0.25;
                    break;
                case ERAState.RollUp:
                    label2.Text = "КРЕН+";
                    cRoll = 0.5 + wf * 0.25;
                    break;
                case ERAState.RollDown:
                    label2.Text = "КРЕН-";
                    cRoll = 0.5 - wf * 0.25;
                    break;
                case ERAState.YawRight:
                    label2.Text = "РЫСК+";
                    cYaw = 0.5 + wf * 0.25;
                    break;
                case ERAState.YawLeft:
                    label2.Text = "РЫСК-";
                    cYaw = 0.5 - wf * 0.25;
                    break;
                default:
                    break;
            }
            if (count>1.5)
            {
                cPitch = cRoll = cYaw = 0.5;
                cThr = 0.5;
                state = ERAState.Idle;
            }
        }
        private void performConversion()
        {
            count += ((double)timer1.Interval) / 1000.0;
            if (copter)
            {
                cConv = count;
            }
            else 
            {
                cConv = 1 - count;
            }
            
            if (count > 1)
            {
                state = ERAState.Idle;
                copter = !copter;
                count = 0;
            }
        }
        private void computeNextValues()
        {
            /*X += 150; if (X > maxval) X = 0;
            Y += 250; if (Y > maxval) Y = 0;
            Z += 350; if (Z > maxval) Z = 0;
            XR += 220; if (XR > maxval) XR = 0;
            ZR += 200; if (ZR > maxval) ZR = 0;*/
            switch(state)
            {
                case ERAState.Disarmed:
                    Arm();
                    label2.Text = "НЕ ВЗВЕДЕНО";
                    break;
                case ERAState.Arming:
                    label2.Text = "ВЗВЕДЕНИЕ";
                    count -= ((double)timer1.Interval) / 1000.0;
                    if (count<0)
                    {
                        ArmReady();
                    }

                    break;
                case ERAState.ThrottleUp:
                    label2.Text = "ПУСК";
                    StartupThrottle();
                    break;
                case ERAState.Idle:
                    label2.Text = "БЕЗДЕЙСТВИЕ";
                    WhatsNext();
                    break;
                case ERAState.Conversion:
                    label2.Text = "КОНВЕРСИЯ";
                    performConversion();
                    break;
                case ERAState.PitchUp:
                case ERAState.PitchDown:
                case ERAState.RollUp:
                case ERAState.RollDown:
                case ERAState.YawLeft:
                case ERAState.YawRight:
                    processStickMovement();
                    break;
                default:
                    cPitch = cRoll = cYaw = 0.5;
                    cThr = 0;
                    break;
            }
        }

        private double generateGaussNoise()
        {
            double sum=0;
            for (int i=0;i!=100;i++)
            {
                Random r = new Random();
                sum+=r.NextDouble()*0.02;
            }
            sum/=100;
            return sum-0.01;
        }
        private void addNoise()
        {
            if (allownoise)
            {
                cThr += generateGaussNoise();
                cPitch += generateGaussNoise();
                cRoll += generateGaussNoise();
                cYaw += generateGaussNoise();
                cConv += generateGaussNoise()/10;
            }
        }
        private void processState()
        {
            setAxes();
            addNoise();
            computeNextValues();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            processState();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            cPitch = cRoll = cYaw = 0.5;
            cThr = 0;
            setAxes();
            allownoise = false;
            state = ERAState.Disarmed;
            timer1.Enabled = false;
        }

        private void FeederForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            button2_Click(this, e);
        }
    }
}
