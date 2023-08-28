#!/usr/bin/bash

(
    docker-compose rm -f
    docker-compose down --rmi all
    docker system prune -f
)