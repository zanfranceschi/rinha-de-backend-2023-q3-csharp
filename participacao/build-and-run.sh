#!/usr/bin/bash

(
    cd ../
    docker build -t rinha-api-2023 -t zanfranceschi/rinha-api-2023 . 
    docker push zanfranceschi/rinha-api-2023:latest

    cd participacao
    docker-compose rm -f
    docker-compose down --rmi all
    docker-compose up --build
)