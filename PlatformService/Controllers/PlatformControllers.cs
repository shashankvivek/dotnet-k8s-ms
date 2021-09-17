using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using PlatformService.AsyncDataServices;
using PlatformService.Data;
using PlatformService.Dtos;
using PlatformService.Models;
using PlatformService.SyncDataServices.Http;

namespace PlatformService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlatformsController: ControllerBase
    {
        private readonly IMessageBusClient _messageBusClient;
        private readonly IPlatformRepo _repository;
        public readonly ICommandDataClient _commandDataClient;
        private readonly IMapper _mapper;

        public PlatformsController(
            IPlatformRepo repository, 
            IMapper mapper,
            ICommandDataClient commandDataClient,
            IMessageBusClient messageBusClient )
        {   _messageBusClient = messageBusClient;
            _repository = repository;
            _commandDataClient = commandDataClient;
            _mapper = mapper;    
        }

        [HttpGet]
        public ActionResult<IEnumerable<PlatformReadDto>> GetPlatforms()
        {
            var platformItem = _repository.GetAllPlatforms();
            return Ok(_mapper.Map<IEnumerable<PlatformReadDto>>(platformItem));
        }

        [HttpGet("{id}", Name = "GetPlatformById")]
        public ActionResult<PlatformReadDto> GetPlatformById(int id)
        {
            var plaformItem = _repository.GetPlatformById(id);
            if (plaformItem != null) 
            {
                return Ok(_mapper.Map<PlatformReadDto>(plaformItem));
            }

            return NotFound();
        }   

        [HttpPost]
        public async Task<ActionResult<PlatformReadDto>> CreatePlatform(PlatformCreateDto platformCreateDto)
        {
            var platformModel = _mapper.Map<Platform>(platformCreateDto);
            _repository.CreatePlatform(platformModel);
            _repository.SaveChanges();

            var platformReadDto = _mapper.Map<PlatformReadDto>(platformModel);

            // send sync message
            try {
                await _commandDataClient.SendPlatformToCommand(platformReadDto);
            } 
            catch (Exception ex) 
            {
                Console.WriteLine(ex);
                Console.WriteLine($"--> could not send synchronously: {ex.Message}");
            }

            // send async messsage
            try
            {
                var platformPublishedDto = _mapper.Map<PlatformPublishedDto>(platformReadDto);
                platformPublishedDto.Event = "Platform Published";
                _messageBusClient.PublishNewPlatform(platformPublishedDto);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"--> Could not send Async: ${ex.Message}");
            }

            return CreatedAtRoute(nameof(GetPlatformById), new {
                id = platformReadDto.Id
            }, platformReadDto);
        }
    }
}