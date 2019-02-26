using System;

namespace AutoMapperTest.ViewModels
{
    public class PersonViewModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public short Age { get; set; }

        public DateTime DateOfBirth { get; set; }
    }
}
