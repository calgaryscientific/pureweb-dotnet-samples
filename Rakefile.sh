require_relative("../../Rakefile-init")

dir = File.dirname(__FILE__)
projects = {
    "DDxService.Cs" => ["./DDxService.Cs", "#{dir}\\DDx\\DDxService2010.sln"],
    "ScribbleApp" => ["./ScribbleApp", "#{dir}\\Scribble\\ScribbleApp2010.sln"]
}
ARCHIVE_PREFIX = "pureweb-sample-DotNet-service-"	

desc "Build, Stage, Package .Net Samples"
task :all => [:setup, :stage, :package, :test]

desc "Package the .Net Samples"
task :package do
	if !Dir.exists?("#{PUREWEB_HOME}/../pkg")
		FileUtils.mkdir "#{PUREWEB_HOME}/../pkg"
	end

	projects.each do |name, project|
		archiveName = "#{ARCHIVE_PREFIX}-#{name}"	
		if !Dir.glob("#{PUREWEB_HOME}/apps/#{project}/").empty?
            archive = OS.windows? ? "\"#{CSI_LIB}\\Tools\\7zip\\7z.exe\" a -tzip #{PUREWEB_HOME}\\..\\pkg\\#{archiveName}.zip #{PUREWEB_HOME}/apps/#{project}/*" :
                "zip -rj #{PUREWEB_HOME}/../pkg/#{archiveName}.zip #{PUREWEB_HOME}/apps/#{project}/"
			sh(archive)
	    end
	end				
end

task :packageclean do
	projects.each do |name, project|
		archiveName = "#{ARCHIVE_PREFIX}-#{name}"	
		if File.exists?("#{PUREWEB_HOME}/../pkg/#{archiveName}.zip")
			puts "deleting #{PUREWEB_HOME}/../pkg/#{archiveName}.zip"
			File.delete "#{PUREWEB_HOME}/../pkg/#{archiveName}.zip"
		end
	end	
end

desc "Upload samples to S3"	
task :upload_to_s3 do
	projectname = File.basename(File.dirname(__FILE__))
	repo_source_description = `git describe --long`.strip().match(/^(?<version>.*?)(-(?<variant>.*?))?-(?<revision>.*?)-(?<hash>.*?)$/)
	version = repo_source_description['version']    

	projects.each do |name, project|    	   
	    puts ("Attempting to uploading #{project} to AWS S3")
		filename = "#{ARCHIVE_PREFIX}-#{name}"
		puts "looking for #{PUREWEB_HOME}/../pkg/#{filename}.zip"
	    if File.exists?("#{PUREWEB_HOME}/../pkg/#{filename}.zip")
	        #upload to the versioned directory
	        sh("aws s3 cp #{PUREWEB_HOME}/../pkg/#{filename}.zip s3://pureweb.io-binaries/continuous/samples/#{projectname}/#{version}/#{repo_source_description}/#{filename}.zip")

	        #given that this should only ever be run from a build machine, we can assume that this build also represents the 'latest' build
	        sh("aws s3 cp s3://pureweb.io-binaries/continuous/samples/#{projectname}/#{version}/#{repo_source_description}/#{filename}.zip s3://pureweb.io-binaries/continuous/samples/#{projectname}/latest/#{filename}.zip")
	    else
	        puts("No file found.  Skipping upload.")
	    end	
	end
end

# Clean files left behind by Visual Studio
def clean_debris 
    objdirs = File.join("**/", "obj")
    userfiles = File.join("**/", "*.vcxproj.user")

    delete_list = FileList.new(objdirs, userfiles)
    delete_list.each do |file|
        puts "Removing #{file}"
        FileUtils.rm_rf("#{file}")
    end
end

#### Task Definintions

desc "Setup the C++ environment"
task :setup do	
    fail("CSI_LIB environment variable is not set. .Net build requires it") if !ENV["CSI_LIB"]
	
	logfiles = File.join("#{BUILD_DIR}/logs", "*samples*.log")
    delete_list = FileList.new(logfiles)
    delete_list.each do |file|
        puts "Removing #{file}"
        FileUtils.rm_rf("#{file}")
    end
	
    puts("Checking for Visual Studio 2010...")
    abort("Can't find a Visual Studio 2010 environment!") if !ENV["VS100COMNTOOLS"]

   	puts "Checking for devenv..."
	abort("Can't find valid devenv 2010 environment!") if !File.exists?("#{DEVENV2010}") 
end

desc "Build .Net Samples"
task :build,[:variant] => [:setup] do |t, args|
	t.invoke_in_scope('build_release_solo')
end


desc "Stage the .Net Samples into #{PUREWEB_HOME}"
task :stage => [@stagedir] do |t|	
	t.invoke_in_scope('build', 'release') # Staging is done by the DeployServer.bat Post-Build
end

task :stageclean do	
	FileUtils.rm_r PUREWEB_HOME + '/apps/DDxService.Cs', :force => true
	FileUtils.rm_r PUREWEB_HOME + '/apps/ScribbleApp', :force => true	
end

desc "Test .Net Samples"
task :test do |t|	
	#Noop
end

# Does not clean everything - must blow away manually
desc "Clean All .net Libraries"
task :clean,[:variant]  do |t, args|
	t.invoke_in_scope('clean_release_solo')
    clean_debris
end

task :build_release => [:build_release_solo]

task :build_release_solo => [:setup] do	
	sh("\"#{DEVENV2010}\" \"#{VS2010_DDX_SLN}\" /Build \"Release|x64\" /Out #{BUILD_DIR.gsub("/","\\")}\\logs\\ddx_debug_solo_2010.log")	
	sh("\"#{DEVENV2010}\" \"#{VS2010_SCRIBBLE_SLN}\" /Build \"Release|x64\" /Out #{BUILD_DIR.gsub("/","\\")}\\logs\\scribble_debug_solo_2010.log")	
end

task :clean_release => [:clean_release_solo]

task :clean_release_solo => [:setup] do
  puts("Cleaning VS2010 Release SDK...")
  sh("\"#{DEVENV2010}\" \"#{VS2010_DDX_SLN}\" /Clean \"Release|x64\" /Out #{BUILD_DIR.gsub("/","\\")}\\logs\\ddx_debug_solo_2010.log")	
  sh("\"#{DEVENV2010}\" \"#{VS2010_SCRIBBLE_SLN}\" /Clean \"Release|x64\" /Out #{BUILD_DIR.gsub("/","\\")}\\logs\\scribble_debug_solo_2010.log")	
end