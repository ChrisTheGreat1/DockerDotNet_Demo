using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerDotNet_Demo
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var client = new DockerClientConfiguration().CreateClient();

            var containerCreationResponse = await client.Containers.CreateContainerAsync(new CreateContainerParameters
            {
                Image = "mongo",
                Name = "mongodb-container",
                ExposedPorts = new Dictionary<string, EmptyStruct>
                    {
                        { "27017/tcp", default(EmptyStruct) }
                    },
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            { "27017/tcp", new List<PortBinding> { new PortBinding { HostPort = "27017" } } }
                        }
                }
            });

            await client.Containers.StartContainerAsync(containerCreationResponse.ID, null);

            var createAndSeedDbCommands = new[]
                {
                    "mongosh",
                    "--eval",
                    "db = db.getSiblingDB('TestDb'); db.TestCollection.insertOne({ id: 1, currentDateTime: new Date() });"
                };

            var createAndSeedDbExecCreateResponse = await client.Exec.ExecCreateContainerAsync(containerCreationResponse.ID,
                new ContainerExecCreateParameters()
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = createAndSeedDbCommands,
                    Tty = false
                });


            await client.Exec.StartAndAttachContainerExecAsync(createAndSeedDbExecCreateResponse.ID, false);

            var retrieveDbContentsCommands = new[]
                {
                    "mongosh",
                    "--eval",
                    "db = db.getSiblingDB('TestDb'); db.TestCollection.find().forEach(printjson);"
                };

            var retrieveDbContentsExecCreateResponse = await client.Exec.ExecCreateContainerAsync(containerCreationResponse.ID,
                new ContainerExecCreateParameters()
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = retrieveDbContentsCommands,
                    Tty = false
                });

            (string stdout, string stderr) result;

            using (var stream = await client.Exec.StartAndAttachContainerExecAsync(retrieveDbContentsExecCreateResponse.ID, false))
            {
                result = await stream.ReadOutputToEndAsync(CancellationToken.None);
            }

            Console.WriteLine("Result:");
            Console.WriteLine(result.stdout);
            Console.WriteLine();
            Console.WriteLine("Errors:");
            Console.WriteLine(result.stderr);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            var container = await client.Containers.InspectContainerAsync(containerCreationResponse.ID);

            var volume = container.Mounts.First().Name;

            Console.WriteLine($"Volume: {volume}");

            await client.Containers.KillContainerAsync(containerCreationResponse.ID, new ContainerKillParameters()); // Stops container.
            await client.Containers.PruneContainersAsync(); // Deletes ALL stopped containers.
        }
    }
}