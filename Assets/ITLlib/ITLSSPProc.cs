using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace ITLlib
{
    
    public class ITLSSPProc
    {
        public const UInt64 MAX_RANDOM_INTEGER = 2147483648;
         RandomNumber rand = new RandomNumber();

        public bool InitiateSSPHostKeys(SSP_KEYS keys , SSP_COMMAND cmd)
        {
	        UInt64 swap = 0;

	        /* create the two random prime numbers  */
	        keys.Generator = rand.GeneratePrime();
	        keys.Modulus = rand.GeneratePrime();
	        /* make sure Generator is larger than Modulus   */
	        if (keys.Generator < keys.Modulus)
	        {
		        swap = keys.Generator;
		        keys.Generator = keys.Modulus;
		        keys.Modulus = swap;
	        }


	        if(CreateHostInterKey(keys) == false)
		        return false;

	        // reset the packet counter here for a successful key neg 
            cmd.encPktCount = 0;

	        return true;
        }

        // Creates a host intermediate key 
        public bool CreateHostInterKey(SSP_KEYS keys)
        {
            if (keys.Generator == 0 || keys.Modulus == 0)
                return false;

            keys.HostRandom  = (UInt64)(rand.GenerateRandomNumber() % MAX_RANDOM_INTEGER);
            keys.HostInter = rand.XpowYmodN(keys.Generator, keys.HostRandom, keys.Modulus);

            return true;
        }

        // creates the host encryption key   
        public bool CreateSSPHostEncryptionKey(SSP_KEYS keys)
        {
	        keys.KeyHost = rand.XpowYmodN(keys.SlaveInterKey,keys.HostRandom,keys.Modulus);

	        return true;
        }

        /* -----------------------------------------------------------------------------------------------------------------*/
        /*    These function are not usually included in the host implementation - they inlcuded here so the user can		*/
        /*	  test the key functions in the host and system	and so the user can impliment them in the slave					*/
        /* -----------------------------------------------------------------------------------------------------------------*/

        /* DLL function call to test user implimentation - emulates slave functions, uses host data to generate HOST and SLAVE keys 
        |  These keys should be the same for a successful key negotation                                            */
        public bool TestSSPSlaveKeys(SSP_KEYS keys)
        {	
	        if(CreateSlaveInterKey(keys) == false)
		        return false;
        		
	        CreateSSPHostEncryptionKey(keys);
	        CreateSlaveEncryptionKey(keys);

	        if(keys.KeyHost == keys.KeySlave)
		        return true;
	        else
		        return false;
        }


        /* Creates a slave encryption key - test function only - this is usually only implimented in the slave */
        public bool CreateSlaveEncryptionKey(SSP_KEYS keys)
        {
	        keys.KeySlave = rand.XpowYmodN(keys.HostInter,keys.SlaveRandom,keys.Modulus);

	        return true;
        }


        /* creates a slave intermediate key - test function only - this is usually implimented only in the slave  */
        public bool CreateSlaveInterKey(SSP_KEYS keys)
        {
	        if ( keys.Generator == 0 || keys.Modulus == 0)
		        return false;

	        keys.SlaveRandom = (UInt64)(rand.GenerateRandomNumber() % MAX_RANDOM_INTEGER);
	        keys.SlaveInterKey = rand.XpowYmodN(keys.Generator,keys.SlaveRandom,keys.Modulus);
	        return true;
        }

    }
}
