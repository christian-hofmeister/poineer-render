// Jenkins Declarative Pipeline for POIneer.Render
// - Builds and tests on 'develop'
// - Creates a published application archive
// - Placeholder deploy stage for 'release/*'
// - Avoids heavy rendering on Jenkins

pipeline {
  agent any

  options {
    ansiColor('xterm')
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
    timeout(time: 30, unit: 'MINUTES')
  }

  environment {
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_ENVIRONMENT = 'Production'

    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"

    DOTNET_SDK_VERSION = '10.0.201'

    RENDER_CSPROJ = 'src/POIneer.Render/POIneer.Render.csproj'
    RENDER_PROJECT_DIR = 'src/POIneer.Render'
    PUBLISH_DIR = 'out/POIneer.Render'

    COVERAGE_MIN = '25'
  }

  parameters {
    booleanParam(
      name: 'RUN_RENDER_CHECK',
      defaultValue: false,
      description: 'Run a quick sanity check on develop. This must not perform heavy rendering.'
    )
  }

  stages {
    stage('Checkout') {
      steps {
        checkout scm
        sh 'git --no-pager log -1 --pretty=fuller'
      }
    }

    stage('Debug dotnet') {
      steps {
        sh '''
          set -eux
          which -a dotnet || true
          echo "PATH=$PATH"
          echo "DOTNET_ROOT=${DOTNET_ROOT-}"
          dotnet --version || true
          dotnet --version
          dotnet --list-sdks
        '''
      }
    }

    stage('Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "${NUGET_PACKAGES}"
          dotnet restore "${RENDER_CSPROJ}" --nologo
        '''
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eux
          dotnet build "${RENDER_CSPROJ}" -c Release --no-restore --nologo
        '''
      }
    }

    stage('Test with Coverage') {
      steps {
        withEnv(['DOTNET_ENVIRONMENT=CI']) {
          sh '''
            set -eux

            echo "WORKSPACE=$WORKSPACE"
            pwd

            echo "Directory overview:"
            find . -maxdepth 3 -type d | sort

            echo "Checking for flyway and java:"
            which flyway || true
            flyway -v || true
            java -version || true

            echo "Running tests with coverage collection..."
            dotnet test POIneerRender.sln -c Release --nologo \
              --results-directory TestResults \
              --logger "junit;LogFilePath=TestResults/test-results.junit.xml" \
              /p:CollectCoverage=true \
              /p:CoverletOutputFormat=cobertura \
              /p:CoverletOutput=TestResults/Coverage/coverage

            echo "[diag] Coverage files:"
            find . -type f -name "coverage.cobertura.xml" -print

            echo "[diag] JUnit files:"
            find . -type f -name "test-results.junit.xml" -print

            echo "[diag] TRX files:"
            find . -type f -name "*.trx" -print
          '''
        }
      }
      post {
        always {
          junit '**/TestResults/**/test-results.junit.xml'

          recordCoverage(
            tools: [[parser: 'COBERTURA', pattern: '**/TestResults/**/coverage.cobertura.xml']],
            sourceCodeRetention: 'LAST_BUILD',
            failOnError: true
          )

          script {
            def min = env.COVERAGE_MIN ? env.COVERAGE_MIN.toInteger() : 25

            def pct = sh(
              script: '''
                set -eu
                f="$(ls -1 **/TestResults/**/coverage.cobertura.xml | head -n1)"
                awk -F'"' "/^<coverage/ {for(i=1;i<=NF;i++){if(\\$i ~ /line-rate=/){print \\$(i+1); exit}}}" "$f" \
                  | awk '{printf("%.0f\\n", $1*100)}'
              ''',
              returnStdout: true
            ).trim()

            echo "Line coverage detected: ${pct}% (threshold: ${min}%)"

            if (pct.isInteger() && pct.toInteger() < min) {
              currentBuild.result = 'FAILURE'
              echo "Coverage is below the configured threshold."
            } else {
              echo "Coverage threshold met."
            }
          }
        }
      }
    }

    stage('Coverage Report (HTML)') {
      when {
        expression { return fileExists('TestResults/Coverage/coverage.cobertura.xml') }
      }
      steps {
        sh '''
          set -eux
          dotnet tool update -g dotnet-reportgenerator-globaltool || dotnet tool install -g dotnet-reportgenerator-globaltool
          export PATH="$HOME/.dotnet/tools:$PATH"

          reportgenerator \
            -reports:**/TestResults/**/coverage.cobertura.xml \
            -targetdir:CoverageReport \
            -reporttypes:Html
        '''
      }
      post {
        always {
          archiveArtifacts artifacts: 'CoverageReport/**', fingerprint: false
        }
      }
    }

    stage('Publish App') {
      steps {
        sh '''
          set -eux
          rm -rf "${PUBLISH_DIR}"

          dotnet publish "${RENDER_CSPROJ}" -c Release -o "${PUBLISH_DIR}" --no-build --nologo

          SAFE_BRANCH="$(echo "$BRANCH_NAME" | tr '/ ' '__')"
          ARCHIVE="poineer-render_${SAFE_BRANCH}.tar.gz"

          tar -C "${PUBLISH_DIR}" -czf "$ARCHIVE" .
          echo "Created archive: $ARCHIVE"
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'poineer-render_*.tar.gz', fingerprint: true
        }
      }
    }

    stage('Deploy Placeholder') {
      when {
        expression {
          return env.BRANCH_NAME ==~ /release\/.+/
        }
      }
      steps {
        sh '''
          set -eux
          echo "[deploy] Release branch detected: ${BRANCH_NAME}"
          echo "[deploy] This is a placeholder. No heavy rendering and no live deploy is performed on Jenkins."
          echo "[deploy] Runtime must set DOTNET_ENVIRONMENT=Production."
          echo "[deploy] Runtime should start from the published app directory or use a predictable working directory."
          echo "[deploy] Later this could rsync the published app to the render server and restart a service."

          # Example:
          # rsync -avz --delete "${PUBLISH_DIR}/" user@render-host:/opt/poineer/render/
          # ssh user@render-host 'sudo systemctl restart poineer-render.service'
        '''
      }
    }

    stage('Optional Dry-Run Render Check') {
      when {
        allOf {
          branch 'develop'
          expression { return params.RUN_RENDER_CHECK == true }
        }
      }
      steps {
        dir("${RENDER_PROJECT_DIR}") {
          withEnv(['DOTNET_ENVIRONMENT=Development']) {
            sh '''
              set -eux
              echo "[check] Running POIneer.Render dry-run check from project directory."
              pwd
              dotnet run -c Release --no-build -- --Renderer:DryRun=true
            '''
          }
        }
      }
    }
  }

  post {
    always {
      script {
        def emoji = currentBuild.currentResult == 'SUCCESS' ? '✅' : (currentBuild.currentResult == 'UNSTABLE' ? '⚠️' : '❌')
        echo "${emoji} Build result: ${currentBuild.currentResult}"
      }
    }
    failure {
      echo "Build failed. See logs above."
    }
  }
}