using AutoMapper;
using MovieManager.BLL.Models;
using MovieManager.DAL.Entities;

namespace MovieManager.PL.API.Configurations
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Actor, ActorModel>().ReverseMap();
            CreateMap<Director, DirectorModel>().ReverseMap();
            CreateMap<Genre, GenreModel>().ReverseMap();
            CreateMap<Movie, MovieModel>().ReverseMap();
            CreateMap<Review, ReviewModel>().ReverseMap();
            CreateMap<MovieActor, MovieActorModel>().ReverseMap();
        }
    }
}
