using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapperTest.Models;
using AutoMapperTest.ViewModels;
namespace AutoMapperTest
{
    public class AutoMapperRegister
    {
        public static void RegisterAutoMapper()
        {
            AutoMapper.Mapper.Initialize((config) =>
            {
                config.CreateMap<PersonModel, PersonViewModel>()
                .ForMember(x=>x.FullName,(target) => 
                {
                    target.ResolveUsing<string>((source) => string.Concat(source.FirstName, " ", source.LastName));
                })
                .ForMember(x=>x.Age, (target) =>
                {
                    target.ResolveUsing<short>((source) => Convert.ToInt16((DateTime.Now.Year - source.DateOfBirth.Year) + 1));
                });
                config.CreateMap<PersonViewModel, PersonModel>();
            });
        }
    }
}
