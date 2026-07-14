using Microsoft.EntityFrameworkCore;
using MovieManager.BLL.Models;
using MovieManager.BLL.Services;
using MovieManager.BLL.Services.Interfaces;
using MovieManager.DAL.Data;
using MovieManager.DAL.Entities;
using MovieManager.DAL.Repositories;
using MovieManager.DAL.Repositories.Interfaces;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Controller + OpenAPI nativo .NET 10 ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// --- DbContext su SQL Server / LocalDB ---
builder.Services.AddDbContext<MovieDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Repository generico + Unit of Work ---
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// --- Generic Service: una registrazione chiusa per ogni entità a chiave singola ---
builder.Services.AddScoped<IGenericService<ActorModel>, GenericService<Actor, ActorModel>>();
builder.Services.AddScoped<IGenericService<DirectorModel>, GenericService<Director, DirectorModel>>();
builder.Services.AddScoped<IGenericService<GenreModel>, GenericService<Genre, GenreModel>>();
builder.Services.AddScoped<IGenericService<MovieModel>, GenericService<Movie, MovieModel>>();
builder.Services.AddScoped<IGenericService<ReviewModel>, GenericService<Review, ReviewModel>>();

// --- Repository + Service dedicati alla chiave composta (MovieActor) ---
builder.Services.AddScoped<IMovieActorRepository, MovieActorRepository>();
builder.Services.AddScoped<IMovieActorService, MovieActorService>();

// --- AutoMapper: cerca i Profile nell'assembly della API (Configurations/MappingProfile) ---
builder.Services.AddAutoMapper(typeof(Program).Assembly);

var app = builder.Build();

// In sviluppo crea il database/schema se non esiste ancora (LocalDB).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MovieDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // documento OpenAPI: /openapi/v1.json
    app.MapScalarApiReference();    // UI interattiva Scalar: /scalar
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
