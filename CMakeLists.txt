cmake_minimum_required(VERSION 2.8.3)
project(sharplink)

## Find catkin macros and libraries
## if COMPONENTS list like find_package(catkin REQUIRED COMPONENTS xyz)
## is used, also find other catkin packages
find_package(catkin REQUIRED)

## System dependencies are found with CMake's conventions

###################################################
## Declare things to be passed to other projects ##
###################################################

## LIBRARIES: libraries you create in this project that dependent projects also need
## CATKIN_DEPENDS: catkin_packages dependent projects also need
## DEPENDS: system dependencies of this project that dependent projects also need
catkin_package()

###########
## Build ##
###########

set(BUILD_CMD "xbuild")
set(BUILD_OPTION "/p:Configuration=Release POSIX")
set(SOLUTION_NAME "${CMAKE_CURRENT_LIST_DIR}/SharpLink.sln")
execute_process(COMMAND ${BUILD_CMD} ${BUILD_OPTION} ${SOLUTION_NAME}
)

# copy to devel
file(GLOB SharpLinkFiles
  "SharpLink/bin/Release/*"
)
file(COPY ${SharpLinkFiles} DESTINATION ${CMAKE_RUNTIME_OUTPUT_DIRECTORY})
