using AutoMapperTest.Models;
using AutoMapperTest.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoMapperTest
{
    class Program
    {
        static void Sample1()
        {
            PersonModel personModel = new PersonModel
            {
                FirstName = "X",
                LastName = "Y",
                DateOfBirth = new DateTime(2010,10,10)
            };
            PersonViewModel personViewModel = AutoMapper.Mapper.Map<PersonModel,PersonViewModel>(personModel);
            PersonModel personModel2 = AutoMapper.Mapper.Map<PersonModel>(personViewModel);
        }
        static void Main(string[] args)
        {
            AutoMapperRegister.RegisterAutoMapper();
            Sample1();
        }
    }
}
