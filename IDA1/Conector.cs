using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IDA1
{
    class Conector
    {
        FTDI myFtdiDevice;

        public Conector()
        {
            // Create new instance of the FTDI device class
            FTDI myFtdiDevice = new FTDI();
        }

        ~Conector()
        {
            FTDI.FT_STATUS ftStatus;
            ftStatus = myFtdiDevice.Close();
        }


        public FTDI.FT_STATUS BuscaIDAS()
        {
            UInt32 ftdiDeviceCount = 0;
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;

            // Determine the number of FTDI devices connected to the machine
            ftStatus = myFtdiDevice.GetNumberOfDevices(ref ftdiDeviceCount);
            // Check status
            if (ftStatus != FTDI.FT_STATUS.FT_OK) return ftStatus;

            // If no devices available, return
            if (ftdiDeviceCount == 0) return FTDI.FT_STATUS.FT_DEVICE_NOT_FOUND;

            // Allocate storage for device info list
            FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

            
            // Populate our device list
            ftStatus = myFtdiDevice.GetDeviceList(ftdiDeviceList);
 
            if (ftStatus == FTDI.FT_STATUS.FT_OK)
            {
                for (UInt32 i = 0; i < ftdiDeviceCount; i++)
                {
                    ftStatus = PreparaConexion(myFtdiDevice, ftdiDeviceList[i].SerialNumber);
                    if (ftStatus != FTDI.FT_STATUS.FT_OK) return ftStatus;

                }
            }

            return FTDI.FT_STATUS.FT_OK;
        }

        private FTDI.FT_STATUS PreparaConexion( FTDI myFtdiDevice, string serial)
        {
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;

            // Open first device in our list by serial number
            ftStatus = myFtdiDevice.OpenBySerialNumber(serial);
            if (ftStatus != FTDI.FT_STATUS.FT_OK) return ftStatus;

            // Set up device data parameters
            // Set Baud rate to 9600
            ftStatus = myFtdiDevice.SetBaudRate(115200);
            if (ftStatus != FTDI.FT_STATUS.FT_OK) return ftStatus;

            // Set data characteristics - Data bits, Stop bits, Parity
            ftStatus = myFtdiDevice.SetDataCharacteristics(FTDI.FT_DATA_BITS.FT_BITS_8, FTDI.FT_STOP_BITS.FT_STOP_BITS_1, FTDI.FT_PARITY.FT_PARITY_NONE);
            if (ftStatus != FTDI.FT_STATUS.FT_OK) return ftStatus;

            // Set flow control - set RTS/CTS flow control
            ftStatus = myFtdiDevice.SetFlowControl(FTDI.FT_FLOW_CONTROL.FT_FLOW_RTS_CTS, 0x11, 0x13);
            if (ftStatus != FTDI.FT_STATUS.FT_OK) return ftStatus;

            // Set read timeout to 5 seconds, write timeout to infinite
            ftStatus = myFtdiDevice.SetTimeouts(5000, 0);
            return ftStatus;
        }
    }

}
