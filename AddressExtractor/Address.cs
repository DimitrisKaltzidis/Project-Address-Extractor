using System.Collections.Generic;

namespace AddressExtractor
{
    public class Address
    {
        // Οδός - Αριθμός
        public string Name { get; set; }

        // Τ.Κ.
        public string ZipCode { get; set; }

        //  Πόλη - Περιοχή
        public string CityArea { get; set; }

        // Νομός
        public string Prefecture { get; set; }
    }
}
