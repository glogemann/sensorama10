using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace sensorama10
{
    class Grove10DoF
    {
        //private const string I2C_CONTROLLER_NAME = "I2C1"; //specific to RPI2
        private const string I2C_CONTROLLER_NAME = "I2C5"; //specific to MBM

        #region BMP180
        private const byte OSS = 0;
        private const byte BMP180_ADDRESS = 0x77;
        private const byte BMP180_ID = 0xD0;

        private short ac1 = 0;
        private short ac2 = 0;
        private short ac3 = 0;
        private ushort ac4 = 0;
        private ushort ac5 = 0;
        private ushort ac6 = 0;
        private short b1 = 0;
        private short b2 = 0;
        private short mb = 0;
        private short mc = 0;
        private short md = 0;

        private long PressureCompensate = 0;

        private I2cDevice BMP180Connection = null;

        private byte[] i2cReadBuffer = new byte[3];

        public Grove10DoF()
        {

        }

        // Read 1 byte from the BMP085 at 'address' 
        // Return: the read byte; 
        private byte BMP180Read(byte adr)
        {
            byte[] i2cBuffer = new byte[1];
            //MSB first return value
            BMP180Connection.Write(new byte[] { adr });
            BMP180Connection.Read(i2cBuffer);
            return i2cBuffer[0];
        }

        // Read 2 bytes from the BMP085 
        // First byte will be from 'address' 
        // Second byte will be from 'address'+1 
        private short BMP180ReadInt(byte adr)
        {
            byte[] i2cBuffer = new byte[2];
            //MSB first return value
            BMP180Connection.WriteRead(new byte[] { adr }, i2cBuffer);
            //BMP180Connection.Read(i2cBuffer);
            short r = (short)((Convert.ToInt16(i2cBuffer[0]) << 8) + Convert.ToInt16(i2cBuffer[1]));
            return r;
        }

        // Read the uncompensated temperature value 
        public async Task<uint> BMP180ReadUT()
        {
            uint ut;
            byte[] i2csBuffer = new byte[2];
            i2csBuffer[0] = 0xF4;
            i2csBuffer[1] = 0x2E;
            BMP180Connection.Write(i2csBuffer);
            await Task.Delay(200);
            ut = (uint)BMP180ReadInt(0xF6);
            return ut;
        }

        public async Task<ulong> BMP180ReadUP()
        {
            byte msb, lsb, xlsb;
            ulong up = 0;
            byte[] i2csBuffer = new byte[2];
            i2csBuffer[0] = 0xF4;
            i2csBuffer[1] = (0x34 + (OSS << 6));
            BMP180Connection.Write(i2csBuffer);
            await Task.Delay(2 + (3 << OSS));
            // Read register 0xF6 (MSB), 0xF7 (LSB), and 0xF8 (XLSB) 
            msb = BMP180Read(0xF6);
            lsb = BMP180Read(0xF7);
            xlsb = BMP180Read(0xF8);
            up = (((ulong)msb << 16) | ((ulong)lsb << 8) | (ulong)xlsb) >> (8 - OSS);
            return up;
        }
        public long BMP180GetPressure(ulong up)
        {
            long x1, x2, x3, b3, b6, p;
            ulong b4, b7;
            b6 = PressureCompensate - 4000;
            x1 = (b2 * (b6 * b6) >> 12) >> 11;
            x2 = (ac2 * b6) >> 11;
            x3 = x1 + x2;
            b3 = (((((long)ac1) * 4 + x3) << OSS) + 2) >> 2;

            // Calculate B4 
            x1 = (ac3 * b6) >> 13;
            x2 = (b1 * ((b6 * b6) >> 12)) >> 16;
            x3 = ((x1 + x2) + 2) >> 2;
            b4 = (ac4 * (ulong)(x3 + 32768)) >> 15;

            b7 = ((ulong)(up - (ulong)b3) * (50000 >> OSS));
            if (b7 < 0x80000000)
                p = (long)((b7 << 1) / b4);
            else
                p = (long)((b7 / b4) << 1);

            x1 = (p >> 8) * (p >> 8);
            x1 = (x1 * 3038) >> 16;
            x2 = (-7357 * p) >> 16;
            p += (x1 + x2 + 3791) >> 4;

            long pressure = p;
            return pressure;
        }

        public float BMP180CalcAltitude(float pressure)
        {
            float A = pressure / 101325;
            float B = 1 / (float)5.25588;
            float C = (float)Math.Pow((double)A, (double)B);
            C = 1 - C;
            C = C / (float)0.0000225577;
            return C;
        }

        public float BMP180GetTemp(uint ut)
        {
            long x1, x2;
            x1 = (((long)ut - (long)ac6) * (long)ac5) >> 15;
            x2 = ((long)mc << 11) / (x1 + md);
            PressureCompensate = x1 + x2;
            float temp = ((PressureCompensate + 8) >> 4);
            temp = temp / 10;
            return temp;
        }

        public async Task<bool> InitSensor() {
            try
            {
                var i2cSettings = new I2cConnectionSettings(BMP180_ADDRESS);
                i2cSettings.BusSpeed = I2cBusSpeed.FastMode;
                string deviceSelector = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);
                var i2cDeviceControllers = await DeviceInformation.FindAllAsync(deviceSelector);
                BMP180Connection = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception: {0}", e.Message);
                return false;
            }

            // check for correct ID 
            BMP180Connection.WriteRead(new byte[] { BMP180_ID }, i2cReadBuffer);
            byte x = i2cReadBuffer[0];
            if (x != 0x55) return false;

            System.Diagnostics.Debug.WriteLine("BMP180 found");

            // load parameter; 
            ac1 = BMP180ReadInt(0xAA);
            ac2 = BMP180ReadInt(0xAC);
            ac3 = BMP180ReadInt(0xAE);
            ac4 = (ushort)BMP180ReadInt(0xB0);
            ac5 = (ushort)BMP180ReadInt(0xB2);
            ac6 = (ushort)BMP180ReadInt(0xB4);
            b1 = BMP180ReadInt(0xB6);
            b2 = BMP180ReadInt(0xB8);
            mb = BMP180ReadInt(0xBA);
            mc = BMP180ReadInt(0xBC);
            md = BMP180ReadInt(0xBE);

            float temp = BMP180GetTemp(await BMP180ReadUT());

            return true;
        }
        #endregion BMP180

        #region MPU9250

        private const byte MPU9250_DEFAULT_ADDRESS = 0x68;
        private const byte MPU9250_MAG_ADDRESS = 0x0C;
        private const byte MPU9250_GYRO_FULL_SCALE_250_DPS = 0x00;
        private const byte MPU9250_GYRO_FULL_SCALE_500_DPS = 0x08;
        private const byte MPU9250_GYRO_FULL_SCALE_1000_DPS = 0x10;
        private const byte MPU9250_GYRO_FULL_SCALE_2000_DPS = 0x18;
        private const byte MPU9250_ACC_FULL_SCALE_2_G = 0x00;
        private const byte MPU9250_ACC_FULL_SCALE_4_G = 0x08;
        private const byte MPU9250_ACC_FULL_SCALE_8_G = 0x10;
        private const byte MPU9250_ACC_FULL_SCALE_16_G = 0x18;
        private const byte MPU9250_RA_WHO_AM_I = 0x75;

        private I2cDevice MPU9250Connection = null;
        private I2cDevice MPU9250MagConnection = null;

        byte MPU9250Read(I2cDevice dev, byte adr)
        {
            byte[] i2cBuffer = new byte[1];
            dev.Write(new byte[] { adr });
            dev.Read(i2cBuffer);
            return i2cBuffer[0];
        }

        bool MPU9250ReadBytes(I2cDevice dev, byte adr, int num, byte[] data)
        {
            byte[] i2cBuffer = new byte[1];
            dev.Write(new byte[] { adr });
            dev.Read(data);
            return true;
        }

        bool MPU9250WriteByte(I2cDevice dev, byte adr, byte data)
        {
            dev.Write(new byte[] { adr });
            dev.Write(new byte[] { data });
            return true;
        }

        public async Task<bool> InitMPU9250Sensor()
        {
            try
            {
                var i2cSettings = new I2cConnectionSettings(MPU9250_MAG_ADDRESS);
                i2cSettings.BusSpeed = I2cBusSpeed.FastMode;
                string deviceSelector = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);
                var i2cDeviceControllers = await DeviceInformation.FindAllAsync(deviceSelector);
                MPU9250MagConnection = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception: {0}", e.Message);
                return false;
            }

            try
            {
                var i2cSettings = new I2cConnectionSettings(MPU9250_DEFAULT_ADDRESS);
                i2cSettings.BusSpeed = I2cBusSpeed.FastMode;
                string deviceSelector = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);
                var i2cDeviceControllers = await DeviceInformation.FindAllAsync(deviceSelector);
                MPU9250Connection = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception: {0}", e.Message);
                return false;
            }


            // check chip Identity
            if (MPU9250Read(MPU9250Connection, MPU9250_RA_WHO_AM_I) != 0x71)
            {
                System.Diagnostics.Debug.WriteLine("MPU9250 Not Found");
                return false;
            };

            // Configure gyroscope range
            MPU9250WriteByte(MPU9250Connection, 27, MPU9250_GYRO_FULL_SCALE_2000_DPS);
            // Configure accelerometers range
            MPU9250WriteByte(MPU9250Connection, 28, MPU9250_ACC_FULL_SCALE_16_G);
            // Set by pass mode for the magnetometers
            MPU9250WriteByte(MPU9250Connection, 0x37, 0x02);

            // Request first magnetometer single measurement
            // TODO: does not work on IOT Core (RPI and MDM) 
            //byte id = MPU9250Read(MPU9250MagConnection, 0x00);
            //MPU9250WriteByte(MPU9250MagConnection, 0x0A, 0x01);

            byte[] Buffer = new byte[14];
            MPU9250ReadBytes(MPU9250Connection, 0x38, 14, Buffer);
            Int16 ax = (Int16)(Buffer[0] << 8 | Buffer[1]);
            Int16 ay = (Int16)(Buffer[2] << 8 | Buffer[3]);
            Int16 az = (Int16)(Buffer[4] << 8 | Buffer[6]);

            return true;
        }

        public Int16 ax = 0;
        public Int16 ay = 0;
        public Int16 az = 0;
        public Int16 gx = 0;
        public Int16 gy = 0;
        public Int16 gz = 0;
        public Int16 temp = 0;
        public async Task<bool> ReadMPU9250Sensor()
        {
            byte[] Buffer = new byte[14];
            MPU9250ReadBytes(MPU9250Connection, 0x3B, 14, Buffer);
            ax = (Int16)(Buffer[0] << 8 | Buffer[1]);
            ay = (Int16)(Buffer[2] << 8 | Buffer[3]);
            az = (Int16)(Buffer[4] << 8 | Buffer[5]);
            temp = (Int16)(Buffer[6] << 8 | Buffer[7]);
            gx = (Int16)(Buffer[8] << 8 | Buffer[9]);
            gy = (Int16)(Buffer[10] << 8 | Buffer[11]);
            gz = (Int16)(Buffer[12] << 8 | Buffer[13]);
            return true; 
        }

        #endregion MPU9250
    }
}
