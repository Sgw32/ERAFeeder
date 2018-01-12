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
        double desThr, desPitch, desRoll, desYaw;
        double centerThrottle = 0.5;
        double count = 0;
        double random_gen = 0;
        Random r;
        double conv_mul = 1.0;
        ERAState state;
        long maxval = 0;
        bool allownoise = false;
        bool copter = true;
        int DiscPovNumber;
        int ContPovNumber;

        enum ERAState { Disarmed, Arming, ThrottleUp, Conversion, Idle, PitchUp, PitchDown,
        RollDown, RollUp, YawLeft, YawRight, StopProcess}; 
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
            r = new Random(DateTime.Now.Millisecond);
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
            desPitch = desRoll = desYaw = 0.5;
            cThr = 0;
            desThr = 0;
            cConv = 0;
            state = ERAState.Disarmed;
            label2.Text = "НЕ ВЗВЕДЕНО";
            joystick.SetBtn(false, id, 1);
        }
        private void updateParameters()
        {
            centerThrottle = ((double)trackBar1.Value / (double)trackBar1.Maximum);
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
            cYaw = 0.5;
            joystick.SetBtn(true, id, 1);
            count = 1.0f;
            state = ERAState.Arming;
        }
        private void ArmReady()
        {
            cYaw = 0.5;
            state = ERAState.ThrottleUp;
            joystick.SetBtn(false, id, 1);
            allownoise = true;
        }
        private void StartupThrottle()
        {
            cThr = cThr + (centerThrottle - cThr) / 2.0 * ((double)timer1.Interval) / 1000.0;
            if (Math.Abs(cThr - centerThrottle) < 0.01)
            {
                count = 0;
                cThr = centerThrottle;
                desThr = centerThrottle * conv_mul;
                state = ERAState.Idle;
            }
        }
        private void makeNewRandomSticks()
        {
            desPitch = 0.5 + generateGaussNoise(3*((double)trackBar2.Value / (double)trackBar2.Maximum), 10);
            desRoll = 0.5 + generateGaussNoise(3 * ((double)trackBar2.Value / (double)trackBar2.Maximum), 10);
            if (!checkBox1.Checked)
                desYaw = 0.5 + generateGaussNoise(3 * ((double)trackBar2.Value / (double)trackBar2.Maximum) * 0.5, 30);
            else
                desYaw = 0.5;
            desThr = centerThrottle * conv_mul + generateGaussNoise(((double)trackBar2.Value / (double)trackBar2.Maximum) * 0.1, 10);
            label2.Text="ОБН "+desPitch.ToString()+" "+desRoll.ToString()+" "+
               desThr.ToString()+" "+desYaw.ToString();
        }
        private void WhatsNext()
        {
            count += ((double)timer1.Interval) / 1000.0;
            cPitch -= (cPitch - desPitch) * 0.05;
            cRoll -= (cRoll - desRoll) * 0.05;
            cThr -= (cThr - desThr) * 0.05;
            cYaw -= (cYaw - desYaw) * 0.05;
            if (count > 3.0f)
            {
                count = 0;
                int rnd_num = (int)Math.Floor(r.NextDouble()*10);
                switch(rnd_num)
                {
                    case 8:
                        state = ERAState.Conversion;
                        break;
                    default:
                        state = ERAState.Idle;
                        makeNewRandomSticks();
                        break;
                }
            }
        }
        private double generateGaussNoise()
        {
            double sum=0;
            double noiseLevel = 0.02 * 0.5;//((double)trackBar2.Value / (double)trackBar2.Maximum);
            for (int i=0;i!=100;i++)
            {
                sum += r.NextDouble() * noiseLevel;
            }
            sum/=100;
            return sum - noiseLevel/2;
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
            cThr -= (cThr - centerThrottle * conv_mul) * 0.05;
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
                    if (!checkBox1.Checked)
                        cYaw = 0.5 + wf * 0.25;
                    break;
                case ERAState.YawLeft:
                    label2.Text = "РЫСК-";
                    if (!checkBox1.Checked)
                        cYaw = 0.5 - wf * 0.25;
                    break;
                default:
                    break;
            }
            if (count>1.5)
            {
                cPitch = cRoll = cYaw = 0.5;
                cThr = centerThrottle * conv_mul;
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
            conv_mul = 1 - cConv * 0.3;
            if (count > 1)
            {
                state = ERAState.Idle;
                copter = !copter;
                count = 0;
            }
        }
        private void addLog()
        {
           
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
                    addLog();
                    break;
                case ERAState.Arming:
                    label2.Text = "ВЗВЕДЕНИЕ";
                    addLog();
                    count -= ((double)timer1.Interval) / 1000.0;
                    if (count<0)
                    {
                        ArmReady();
                    }

                    break;
                case ERAState.ThrottleUp:
                    label2.Text = "ПУСК";
                    addLog();
                    StartupThrottle();
                    break;
                case ERAState.Idle:
                    label2.Text = "БЕЗДЕЙСТВИЕ";
                    addLog();
                    WhatsNext();
                    break;
                case ERAState.Conversion:
                    label2.Text = "КОНВЕРСИЯ";
                    addLog();
                    performConversion();
                    break;
                case ERAState.StopProcess:
                    label2.Text = "СТОП ГОТОВ";
                    cPitch = cRoll = cYaw = 0.5;
                    desYaw = desPitch = desRoll = 0.5;
                    desThr = centerThrottle * conv_mul;
                    cConv = 0;
                    cThr = centerThrottle * conv_mul;
                    break;
                case ERAState.PitchUp:
                case ERAState.PitchDown:
                case ERAState.RollUp:
                case ERAState.RollDown:
                case ERAState.YawLeft:
                case ERAState.YawRight:
                    processStickMovement();
                    addLog();
                    break;
                default:
                    cPitch = cRoll = cYaw = 0.5;
                    cThr = 0;
                    break;
            }
        }

        
        private double generateGaussNoise(double noiseLevel,int it)
        {
            double sum = 0;
            for (int i = 0; i != it; i++)
            {
                sum += r.NextDouble() * noiseLevel;
            }
            sum /= it;
            return sum - noiseLevel / 2;
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
            updateParameters();
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
            label2.Text = "СТОП";
        }

        private void FeederForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            button2_Click(this, e);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }

        private void label2_TextChanged(object sender, EventArgs e)
        {
            DateTime thisDay = DateTime.Now;
            textBox1.Text += thisDay.ToString() + " СТАТУС:" + label2.Text + Environment.NewLine;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            cYaw = 0.5;
            desYaw = 0.5;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            state = ERAState.StopProcess;
        }
    }
}
